using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
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
/// 下载指定版本安装包。
///
/// 必须登录。
///
/// 客户用户：
/// 必须拥有当前软件的有效绑定关系。
/// </summary>
[Authorize]
[HttpGet("{id}/package/download")]
public async Task<IActionResult> DownloadPackage(int id)
{
    /*
     * =========================
     * 1. 获取当前登录用户ID
     * =========================
     *
     * 这个 Claim 是我们生成 JWT 时放进去的：
     *
     * ClaimTypes.NameIdentifier
     */
    var userIdText =
        User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

    if (!int.TryParse(
            userIdText,
            out var userId))
    {
        return Unauthorized();
    }


    /*
     * =========================
     * 2. 再从数据库读取当前用户
     * =========================
     *
     * 为什么 JWT 已经有用户信息，
     * 还要再查数据库？
     *
     * 因为用户可能在 Token 签发以后：
     *
     * - 被管理员停用
     * - 客户被停用
     * - 软件授权被取消
     *
     * 权限属于比较敏感的数据，
     * 下载时重新检查数据库更可靠。
     */
    var currentUser =
        await _dbContext.Users
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(
                x => x.Id == userId
            );

    if (currentUser == null)
    {
        return Unauthorized();
    }

    if (!currentUser.IsEnabled)
    {
        return Forbid();
    }


    /*
     * =========================
     * 3. 查询软件版本 + 所属软件
     * =========================
     */
    var softwareVersion =
        await _dbContext.SoftwareVersions
            .Include(x => x.Software)
            .FirstOrDefaultAsync(
                x => x.Id == id
            );

    if (softwareVersion == null)
    {
        return NotFound(
            "软件版本不存在"
        );
    }

    if (softwareVersion.Software == null)
    {
        return NotFound(
            "所属软件不存在"
        );
    }


    /*
     * =========================
     * 4. 软件级总开关
     * =========================
     */

    // 软件已经停用
    if (!softwareVersion.Software.IsEnabled)
    {
        return Forbid();
    }

    // 软件禁止下载
    if (!softwareVersion.Software.AllowDownload)
    {
        return Forbid();
    }

    // 当前版本禁止下载
    if (!softwareVersion.AllowDownload)
    {
        return Forbid();
    }


    /*
     * =========================
     * 5. Customer 用户额外检查授权
     * =========================
     */
    if (currentUser.Role == "Customer")
    {
        /*
         * 客户用户必须属于某个客户。
         */
        if (!currentUser.CustomerId.HasValue)
        {
            return Forbid();
        }

        /*
         * 所属客户必须存在并且启用。
         */
        if (
            currentUser.Customer == null ||
            !currentUser.Customer.IsEnabled
        )
        {
            return Forbid();
        }

        /*
         * 检查：
         *
         * 当前客户
         * +
         * 当前软件
         *
         * 是否存在有效 CustomerSoftware。
         */
        var hasSoftwarePermission =
            await _dbContext.CustomerSoftwares
                .AnyAsync(
                    x =>
                        x.CustomerId ==
                        currentUser.CustomerId.Value
                        &&
                        x.SoftwareId ==
                        softwareVersion.SoftwareId
                        &&
                        x.IsEnabled
                );

        if (!hasSoftwarePermission)
        {
            /*
             * 已经知道你是谁，
             * 但是你没有这个软件的权限。
             *
             * 所以这里是：
             *
             * 403 Forbidden
             *
             * 而不是401。
             */
            return Forbid();
        }
    }


    /*
     * =========================
     * 6. 检查安装包
     * =========================
     */
    if (string.IsNullOrWhiteSpace(
            softwareVersion.PackageRelativePath))
    {
        return NotFound(
            "当前版本尚未上传安装包"
        );
    }


    /*
     * 拼接服务器真实路径
     */
    var fullPath =
        Path.Combine(
            _environment.ContentRootPath,
            softwareVersion
                .PackageRelativePath
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar
                )
        );


    /*
     * 数据库有记录，
     * 但磁盘文件也可能被人为删除。
     */
    if (!System.IO.File.Exists(fullPath))
    {
        return NotFound(
            "安装包文件不存在，请重新上传"
        );
    }


    /*
     * 用户最终看到的下载文件名。
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
     * =========================
     * 7. FileStream流式下载
     * =========================
     */
    var fileStream =
        new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );


    return File(
        fileStream,
        "application/octet-stream",
        downloadFileName,
        enableRangeProcessing: true
    );
}
    }

}

 