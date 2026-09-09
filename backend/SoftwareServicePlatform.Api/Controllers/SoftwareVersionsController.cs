using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using System.Security.Cryptography;
namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 软件版本管理接口
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SoftwareVersionsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        private readonly IWebHostEnvironment _environment;
        /// <summary>
        /// 通过依赖注入获取数据库上下文
        /// </summary>
        public SoftwareVersionsController(AppDbContext dbContext, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }

        /// <summary>
        /// 获取版本列表
        ///
        /// 调用方式：
        ///
        /// 获取所有版本：
        /// GET /api/softwareversions
        ///
        /// 获取某个软件的版本：
        /// GET /api/softwareversions?softwareId=1
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSoftwareVersions(
            [FromQuery] int? softwareId)
        {
            // 先得到 SoftwareVersions 的查询对象
            var query = _dbContext.SoftwareVersions.AsQueryable();

            // 如果前端传了 softwareId，
            // 就只查询这个软件下面的版本
            if (softwareId.HasValue)
            {
                query = query.Where(
                    x => x.SoftwareId == softwareId.Value
                );
            }

            // 最新创建的版本显示在最上面
            var versions = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(versions);
        }

        /// <summary>
        /// 新增软件版本
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateSoftwareVersion(
            SoftwareVersion softwareVersion)
        {
            // 1. 必须选择一个软件
            if (softwareVersion.SoftwareId <= 0)
            {
                return BadRequest("请选择所属软件");
            }

            // 2. 版本号不能为空
            if (string.IsNullOrWhiteSpace(
                softwareVersion.Version))
            {
                return BadRequest("版本号不能为空");
            }

            // 3. 检查软件是否真的存在
            var softwareExists =
                await _dbContext.Softwares.AnyAsync(
                    x => x.Id == softwareVersion.SoftwareId
                );

            if (!softwareExists)
            {
                return BadRequest("所属软件不存在");
            }

            // 4. 同一个软件不能存在两个相同版本号
            //
            // 例如：
            // 路面检测软件 1.0.0
            // 路面检测软件 1.0.0
            //
            // 这种情况不允许
            var versionExists =
                await _dbContext.SoftwareVersions.AnyAsync(
                    x =>
                        x.SoftwareId
                            == softwareVersion.SoftwareId
                        &&
                        x.Version
                            == softwareVersion.Version
                );

            if (versionExists)
            {
                return BadRequest(
                    "该软件已经存在相同版本号"
                );
            }

            // 5. 如果用户勾选“已发布”，
            // 自动记录发布时间
            if (softwareVersion.IsPublished)
            {
                softwareVersion.PublishedAt =
                    DateTime.UtcNow;
            }
            else
            {
                softwareVersion.PublishedAt = null;
            }

            // 6. 创建时间和修改时间由服务器负责
            softwareVersion.CreatedAt =
                DateTime.UtcNow;

            softwareVersion.UpdatedAt =
                DateTime.UtcNow;

            // 7. 添加到数据库
            _dbContext.SoftwareVersions.Add(
                softwareVersion
            );

            await _dbContext.SaveChangesAsync();

            return Ok(softwareVersion);
        }

        /// <summary>
        /// 修改软件版本
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSoftwareVersion(
            int id,
            SoftwareVersion softwareVersion)
        {
            // 1. 根据 ID 查找原始版本记录
            var existingVersion =
                await _dbContext.SoftwareVersions
                    .FindAsync(id);

            if (existingVersion == null)
            {
                return NotFound("软件版本不存在");
            }

            // 2. 检查软件ID
            if (softwareVersion.SoftwareId <= 0)
            {
                return BadRequest("请选择所属软件");
            }

            // 3. 检查版本号
            if (string.IsNullOrWhiteSpace(
                softwareVersion.Version))
            {
                return BadRequest("版本号不能为空");
            }

            // 4. 确认所属软件存在
            var softwareExists =
                await _dbContext.Softwares.AnyAsync(
                    x => x.Id == softwareVersion.SoftwareId
                );

            if (!softwareExists)
            {
                return BadRequest("所属软件不存在");
            }

            // 5. 检查版本号重复
            //
            // x.Id != id 很重要：
            // 修改自己时不能把自己判断成重复数据
            var versionExists =
                await _dbContext.SoftwareVersions.AnyAsync(
                    x =>
                        x.SoftwareId
                            == softwareVersion.SoftwareId
                        &&
                        x.Version
                            == softwareVersion.Version
                        &&
                        x.Id != id
                );

            if (versionExists)
            {
                return BadRequest(
                    "该软件已经存在相同版本号"
                );
            }

            // 6. 更新允许修改的字段
            existingVersion.SoftwareId =
                softwareVersion.SoftwareId;

            existingVersion.Version =
                softwareVersion.Version;

            existingVersion.VersionType =
                softwareVersion.VersionType;

            existingVersion.Title =
                softwareVersion.Title;

            existingVersion.ReleaseNotes =
                softwareVersion.ReleaseNotes;

            existingVersion.ForceUpdate =
                softwareVersion.ForceUpdate;

            existingVersion.AllowDownload =
                softwareVersion.AllowDownload;

            /*
             * 处理发布状态。
             *
             * 情况1：
             * 原来未发布，现在改成发布
             * -> 自动记录发布时间
             *
             * 情况2：
             * 原来已经发布
             * -> 保留原发布时间
             *
             * 情况3：
             * 改成未发布
             * -> 清空发布时间
             */
            if (softwareVersion.IsPublished)
            {
                if (!existingVersion.IsPublished)
                {
                    existingVersion.PublishedAt =
                        DateTime.UtcNow;
                }
            }
            else
            {
                existingVersion.PublishedAt = null;
            }

            existingVersion.IsPublished =
                softwareVersion.IsPublished;

            // 修改时间重新记录
            existingVersion.UpdatedAt =
                DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return Ok(existingVersion);
        }

        /// <summary>
        /// 删除软件版本
        ///
        /// 除了删除数据库记录以外，
        /// 如果该版本已经上传过安装包，
        /// 还需要清理服务器上的版本文件目录。
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSoftwareVersion(int id)
        {
            /*
             * 1. 查询版本是否存在
             */
            var softwareVersion =
                await _dbContext.SoftwareVersions
                    .FindAsync(id);

            if (softwareVersion == null)
            {
                return NotFound("软件版本不存在");
            }

            /*
             * 2. 计算这个版本对应的安装包目录。
             *
             * 例如版本 ID = 3：
             *
             * storage
             * └─ software-packages
             *    └─ 3
             */
            var packageDirectory = Path.Combine(
                _environment.ContentRootPath,
                "storage",
                "software-packages",
                id.ToString()
            );

            /*
             * 3. 先删除数据库记录。
             *
             * 为什么不是先删除硬盘文件？
             *
             * 假如我们先把安装包删掉了，
             * 但 SaveChangesAsync() 又失败，
             *
             * 就会出现：
             *
             * 数据库还说“有这个版本和安装包”
             * 但实际文件已经不存在了。
             *
             * 这个问题比留下一个孤儿文件更严重。
             */
            _dbContext.SoftwareVersions.Remove(
                softwareVersion
            );

            await _dbContext.SaveChangesAsync();

            /*
             * 4. 数据库删除成功以后，
             * 再清理服务器文件。
             *
             * true：
             * 表示连同目录下所有文件一起删除。
             */
            try
            {
                if (Directory.Exists(packageDirectory))
                {
                    Directory.Delete(
                        packageDirectory,
                        recursive: true
                    );
                }
            }
            catch (Exception ex)
            {
                /*
                 * 文件清理失败不能让已经成功的数据库删除
                 * 再假装失败。
                 *
                 * 目前学习阶段先记录日志。
                 *
                 * 后续正式系统还可以增加：
                 * - 后台垃圾文件清理任务
                 * - 管理员告警
                 */
                Console.WriteLine(
                    $"删除版本安装包目录失败：{ex.Message}"
                );
            }

            return NoContent();
        }

        /// <summary>
        /// 给指定的软件版本上传安装包
        ///
        /// 请求方式：
        /// POST /api/softwareversions/{id}/package
        ///
        /// 请求格式：
        /// multipart/form-data
        ///
        /// 表单字段：
        /// file
        /// </summary>
        [HttpPost("{id}/package")]
        [RequestSizeLimit(2L * 1024 * 1024 * 1024)]
        public async Task<IActionResult> UploadPackage(
            int id,
            [FromForm] IFormFile file)
        {
            /*
             * 1. 先查版本是否存在
             */
            var softwareVersion =
                await _dbContext.SoftwareVersions
                    .FindAsync(id);

            if (softwareVersion == null)
            {
                return NotFound("软件版本不存在");
            }

            /*
             * 2. 检查文件
             */
            if (file == null || file.Length <= 0)
            {
                return BadRequest("请选择要上传的安装包");
            }

            /*
             * 3. 获取扩展名
             *
             * 例如：
             * Setup.exe
             *
             * 得到：
             * .exe
             */
            var extension =
                Path.GetExtension(file.FileName)
                    .ToLowerInvariant();

            /*
             * 当前阶段只允许这几种安装包。
             *
             * 后续如果需要：
             * .7z
             * .rar
             *
             * 再增加即可。
             */
            var allowedExtensions = new[]
            {
        ".exe",
        ".msi",
        ".zip"
    };

            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(
                    "目前只允许上传 exe、msi 或 zip 文件"
                );
            }

            /*
             * 4. 获取安全的原始文件名
             *
             * Path.GetFileName 可以避免用户文件名
             * 携带目录路径。
             */
            var originalFileName =
                Path.GetFileName(file.FileName);

            /*
             * 5. 每一个软件版本建立独立目录
             *
             * 例如版本ID = 12：
             *
             * storage
             * └─ software-packages
             *    └─ 12
             */
            var packageDirectory = Path.Combine(
                _environment.ContentRootPath,
                "storage",
                "software-packages",
                id.ToString()
            );

            Directory.CreateDirectory(
                packageDirectory
            );

            /*
             * 6. 磁盘上不用用户原始文件名，
             * 而使用 GUID。
             *
             * 例如：
             *
             * 原文件：
             * RoadProcess_Setup.exe
             *
             * 磁盘实际保存：
             * 4938109ff4....exe
             *
             * 这样可以避免：
             * - 文件重名
             * - 特殊字符
             * - 路径问题
             */
            var storedFileName =
                $"{Guid.NewGuid():N}{extension}";

            var fullPath = Path.Combine(
                packageDirectory,
                storedFileName
            );

            /*
             * 7. 把上传的数据真正写入硬盘
             */
            await using (
                var fileStream =
                    new FileStream(
                        fullPath,
                        FileMode.Create
                    )
            )
            {
                await file.CopyToAsync(
                    fileStream
                );
            }

            /*
             * 8. 计算 SHA256
             *
             * SHA256 可以用来判断：
             *
             * 安装包有没有损坏
             * 安装包内容有没有变化
             */
            string sha256Text;

            using (
                var sha256 = SHA256.Create()
            )
            {
                await using var hashStream =
                    System.IO.File.OpenRead(
                        fullPath
                    );

                var hashBytes =
                    await sha256.ComputeHashAsync(
                        hashStream
                    );

                sha256Text =
                    Convert.ToHexString(
                        hashBytes
                    ).ToLowerInvariant();
            }

            /*
             * 9. 如果这个版本以前已经上传过安装包，
             * 删除旧文件。
             *
             * 注意：
             * 新文件已经保存成功之后，
             * 我们才删除旧文件。
             */
            if (!string.IsNullOrWhiteSpace(
                    softwareVersion.PackageRelativePath))
            {
                var oldFullPath =
                    Path.Combine(
                        _environment.ContentRootPath,
                        softwareVersion
                            .PackageRelativePath
                            .Replace(
                                '/',
                                Path.DirectorySeparatorChar
                            )
                    );

                if (System.IO.File.Exists(
                        oldFullPath))
                {
                    System.IO.File.Delete(
                        oldFullPath
                    );
                }
            }

            /*
             * 10. 保存相对路径
             *
             * 数据库不要保存：
             *
             * D:\job\工作COD\...
             *
             * 因为以后部署到 Linux，
             * 这个路径肯定会变化。
             *
             * 数据库只保存相对路径。
             */
            var relativePath =
                Path.Combine(
                    "storage",
                    "software-packages",
                    id.ToString(),
                    storedFileName
                )
                .Replace("\\", "/");

            /*
             * 11. 保存安装包信息到数据库
             */
            softwareVersion.PackageFileName =
                originalFileName;

            softwareVersion.PackageFileSize =
                file.Length;

            softwareVersion.PackageRelativePath =
                relativePath;

            softwareVersion.PackageSha256 =
                sha256Text;

            softwareVersion.PackageUploadedAt =
                DateTime.UtcNow;

            softwareVersion.UpdatedAt =
                DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            /*
             * 12. 返回上传结果
             */
            return Ok(new
            {
                message = "安装包上传成功",

                softwareVersion.Id,

                softwareVersion.PackageFileName,

                softwareVersion.PackageFileSize,

                softwareVersion.PackageSha256,

                softwareVersion.PackageUploadedAt
            });
        }
        /// <summary>
        /// 下载指定软件版本的安装包
        ///
        /// 请求：
        /// GET /api/softwareversions/{id}/package/download
        /// </summary>
        [HttpGet("{id}/package/download")]
        public async Task<IActionResult> DownloadPackage(int id)
        {
            /*
             * 1. 查找软件版本
             */
            var softwareVersion =
     await _dbContext.SoftwareVersions
         .Include(x => x.Software)
         .FirstOrDefaultAsync(x => x.Id == id);

            if (softwareVersion == null)
            {
                return NotFound("软件版本不存在");
            }

            /*
   * 所属软件不存在。
   *
   * 正常情况下因为外键关系不应该出现，
   * 但接口仍然做保护。
   */
            if (softwareVersion.Software == null)
            {
                return BadRequest("所属软件不存在");
            }

            /*
             * 软件已经被停用。
             *
             * Software.IsEnabled 是软件级总开关。
             * 一旦软件停用，下面所有版本都不能下载。
             */
            if (!softwareVersion.Software.IsEnabled)
            {
                return BadRequest("当前软件已停用，禁止下载安装包");
            }

            /*
             * 软件级禁止下载。
             *
             * 即使某个版本自己的 AllowDownload = true，
             * 也不能突破软件级总开关。
             */
            if (!softwareVersion.Software.AllowDownload)
            {
                return BadRequest("当前软件已禁止下载");
            }

            /*
             * 软件允许下载后，
             * 再判断当前具体版本是否允许下载。
             */
            if (!softwareVersion.AllowDownload)
            {
                return BadRequest("当前版本不允许下载");
            }

            /*
             * 2. 判断这个版本是否允许下载
             */
            if (!softwareVersion.AllowDownload)
            {
                return BadRequest("当前版本不允许下载");
            }

            /*
             * 3. 判断有没有上传安装包
             */
            if (string.IsNullOrWhiteSpace(
                    softwareVersion.PackageRelativePath))
            {
                return NotFound("当前版本尚未上传安装包");
            }

            /*
             * 4. 根据数据库里的相对路径，
             * 拼出服务器上的真实文件路径。
             */
            var fullPath = Path.Combine(
                _environment.ContentRootPath,
                softwareVersion
                    .PackageRelativePath
                    .Replace(
                        '/',
                        Path.DirectorySeparatorChar
                    )
            );

            /*
             * 5. 数据库虽然有记录，
             * 但是实际文件也可能被人为删除。
             *
             * 所以必须再次检查文件是否真的存在。
             */
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(
                    "安装包文件不存在，请重新上传"
                );
            }

            /*
             * 6. 获取下载时显示给用户的文件名。
             *
             * 磁盘上的文件可能叫：
             * 8fbb2d11cxxx.exe
             *
             * 但用户下载时应该看到：
             * RoadProcess_Setup_1.0.0.exe
             */
            var downloadFileName =
                softwareVersion.PackageFileName;

            if (string.IsNullOrWhiteSpace(
                    downloadFileName))
            {
                downloadFileName =
                    Path.GetFileName(fullPath);
            }

            /*
             * 7. 以文件流方式返回。
             *
             * 不要先把整个安装包读取到 byte[]。
             *
             * 因为以后文件可能：
             * 500MB
             * 1GB
             * 2GB
             *
             * FileStream 可以边读取边发送。
             */
            var fileStream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

            /*
             * application/octet-stream
             *
             * 表示这是一个普通二进制文件。
             *
             * enableRangeProcessing = true
             * 允许浏览器使用 Range 请求，
             * 对大文件下载更友好。
             */
            return File(
                fileStream,
                "application/octet-stream",
                downloadFileName,
                enableRangeProcessing: true
            );
        }
    }

}

 