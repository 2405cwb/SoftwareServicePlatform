using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 客户门户 - 我的软件
    ///
    /// 这个 Controller 专门给 Customer 用户使用。
    ///
    /// 注意：
    /// 客户不能自己传 CustomerId，
    /// 后端根据 JWT 中的当前用户身份，
    /// 自动找到这个用户所属的客户。
    /// </summary>
    [ApiController]
    [Route("api/my-software")]
    [Authorize(Roles = "Customer")]
    public class MySoftwareController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;
        public MySoftwareController(
            AppDbContext dbContext, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }


        /// <summary>
        /// 获取当前登录客户已经授权的软件。
        ///
        /// GET:
        /// /api/my-software
        ///
        /// 不需要：
        /// ?customerId=xxx
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMySoftware()
        {
            /*
             * ==========================================
             * 1. 从 JWT 中取得当前登录用户的 UserId
             * ==========================================
             *
             * 登录时我们在 JWT 中写入了：
             *
             * ClaimTypes.NameIdentifier
             *
             * 内容就是 User.Id。
             */
            var userIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );


            /*
             * JWT 中没有用户ID，
             * 说明 Token 数据不完整。
             */
            if (!int.TryParse(
                userIdText,
                out var userId))
            {
                return Unauthorized();
            }


            /*
             * ==========================================
             * 2. 根据 UserId 到数据库查询当前用户
             * ==========================================
             *
             * 为什么不直接相信 JWT 里的 CustomerId？
             *
             * 因为数据库中的用户关系可能被管理员修改。
             *
             * 例如：
             *
             * 原来：
             * CustomerId = 1
             *
             * 后来管理员修改为：
             * CustomerId = 2
             *
             * 旧 JWT 里面可能还是 1。
             *
             * 所以：
             *
             * JWT 负责告诉我们“你是谁”
             * 数据库负责告诉我们“你现在属于谁”
             */
            var currentUser =
                await _dbContext.Users
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(
                        x => x.Id == userId
                    );


            /*
             * 用户已经不存在。
             */
            if (currentUser == null)
            {
                return Unauthorized();
            }


            /*
             * 用户已经被管理员停用。
             */
            if (!currentUser.IsEnabled)
            {
                return Forbid();
            }


            /*
             * Customer 用户必须绑定 Customer。
             */
            if (currentUser.CustomerId == null)
            {
                return Forbid();
            }


            /*
             * 所属客户必须存在。
             */
            if (currentUser.Customer == null)
            {
                return Forbid();
            }


            /*
             * 所属客户已经停用，
             * 也不允许继续使用客户门户。
             */
            if (!currentUser.Customer.IsEnabled)
            {
                return Forbid();
            }


            /*
             * ==========================================
             * 3. 查询这个客户绑定的软件
             * ==========================================
             *
             * 只查询：
             *
             * CustomerSoftware.IsEnabled = true
             *
             * 并且：
             *
             * Software.IsEnabled = true
             */
            var bindings =
                await _dbContext.CustomerSoftwares

                    .AsNoTracking()

                    .Include(
                        x => x.Software
                    )

                    .ThenInclude(
                        x => x!.Versions
                    )

                    .Where(
                        x =>
                            x.CustomerId ==
                            currentUser.CustomerId.Value
                            &&
                            x.IsEnabled
                            &&
                            x.Software != null
                            &&
                            x.Software.IsEnabled
                    )

                    .OrderBy(
                        x => x.Software!.Name
                    )

                    .ToListAsync();


            /*
             * ==========================================
             * 4. 整理给前端的数据
             * ==========================================
             */
            var result =
                bindings.Select(
                    binding =>
                    {
                        var software =
                            binding.Software!;


                        /*
                         * 客户只看正式发布的 Release。
                         *
                         * Beta / Dev 暂时不向客户展示。
                         */
                        var latestVersion =
                            software.Versions

                                .Where(
                                    version =>
                                        version.IsPublished
                                        &&
                                        version.VersionType
                                        == "Release"
                                )

                                .OrderByDescending(
                                    version =>
                                        version.PublishedAt
                                        ??
                                        version.CreatedAt
                                )

                                .ThenByDescending(
                                    version =>
                                        version.Id
                                )

                                .FirstOrDefault();


                        return new
                        {
                            /*
                             * 软件信息
                             */
                            SoftwareId =
                                software.Id,

                            SoftwareName =
                                software.Name,

                            SoftwareCode =
                                software.Code,

                            ShortName =
                                software.ShortName,

                            Category =
                                software.Category,

                            Description =
                                software.Description,

                            Platform =
                                software.Platform,

                            /*
                             * 客户的软件绑定时间
                             */
                            BoundAt =
                                binding.BoundAt,


                            /*
                             * 最新正式版本。
                             *
                             * 如果还没有正式版本：
                             *
                             * LatestVersion = null
                             */
                            LatestVersion =
                                latestVersion == null
                                    ? null
                                    : new
                                    {
                                        latestVersion.Id,

                                        latestVersion.Version,

                                        latestVersion.Title,

                                        latestVersion.ReleaseNotes,

                                        latestVersion.PublishedAt,

                                        latestVersion.ForceUpdate,

                                        latestVersion.PackageFileName,

                                        latestVersion.PackageFileSize,

                                        /*
                                         * 最终是否允许客户下载。
                                         *
                                         * 软件级开关
                                         * +
                                         * 版本级开关
                                         * +
                                         * 已上传安装包
                                         */
                                        CanDownload =
                                            software.AllowDownload
                                            &&
                                            latestVersion.AllowDownload
                                            &&
                                            !string.IsNullOrWhiteSpace(
                                                latestVersion.PackageRelativePath
                                            )
                                    }
                        };
                    }
                );


            return Ok(result);
        }

        /// <summary>
        /// 获取当前 Customer 用户所属的 CustomerId。
        ///
        /// 返回 null 表示：
        ///
        /// 用户不存在
        /// 用户已停用
        /// 没有关联客户
        /// 客户已停用
        /// </summary>
        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            /*
             * 从 JWT 中得到当前 User.Id。
             */
            var userIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );


            if (!int.TryParse(
                    userIdText,
                    out var userId))
            {
                return null;
            }


            /*
             * 查询当前用户。
             */
            var currentUser =
                await _dbContext.Users

                    .AsNoTracking()

                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == userId
                            &&
                            x.IsEnabled
                    );


            if (
                currentUser == null
                ||
                !currentUser.CustomerId.HasValue
            )
            {
                return null;
            }


            /*
             * 客户本身也必须仍然启用。
             */
            var customerEnabled =
                await _dbContext.Customers

                    .AsNoTracking()

                    .AnyAsync(
                        x =>
                            x.Id ==
                            currentUser.CustomerId.Value
                            &&
                            x.IsEnabled
                    );


            if (!customerEnabled)
            {
                return null;
            }


            return currentUser.CustomerId.Value;
        }/// <summary>
         /// 客户查询某个软件版本可见的资料。
         ///
         /// GET
         /// /api/my-software/versions/{versionId}/attachments
         ///
         /// Customer 只能看到：
         ///
         /// 1. 自己公司已经授权的软件
         /// 2. 正式发布的 Release 版本
         /// 3. IsCustomerVisible = true 的资料
         /// </summary>
        [HttpGet("versions/{versionId:int}/attachments")]
        public async Task<IActionResult> GetVersionAttachments(
            int versionId)
        {
            /*
             * 获取当前客户。
             */
            var customerId =
                await GetCurrentCustomerIdAsync();


            if (!customerId.HasValue)
            {
                return Forbid();
            }


            /*
             * 客户门户只允许访问：
             *
             * 已发布
             * Release
             *
             * 的版本。
             */
            var softwareVersion =
                await _dbContext.SoftwareVersions

                    .AsNoTracking()

                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == versionId
                            &&
                            x.IsPublished
                            &&
                            x.VersionType == "Release"
                    );


            if (softwareVersion == null)
            {
                return NotFound(
                    "软件版本不存在或尚未发布"
                );
            }


            /*
             * 检查这个客户是否真的拥有
             * 这个版本所属的软件。
             *
             * 不能因为客户知道 VersionId，
             * 就让他查看其他客户的软件资料。
             */
            var hasSoftwarePermission =
                await _dbContext.CustomerSoftwares

                    .AsNoTracking()

                    .AnyAsync(
                        x =>
                            x.CustomerId ==
                            customerId.Value
                            &&
                            x.SoftwareId ==
                            softwareVersion.SoftwareId
                            &&
                            x.IsEnabled
                            &&
                            x.Software != null
                            &&
                            x.Software.IsEnabled
                    );


            if (!hasSoftwarePermission)
            {
                return Forbid();
            }


            /*
             * 最关键的过滤：
             *
             * IsCustomerVisible == true
             *
             * 问题日志等内部文件
             * 在这里直接被服务器过滤掉。
             */
            var attachments =
                await _dbContext
                    .SoftwareVersionAttachments

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.SoftwareVersionId ==
                            versionId
                            &&
                            x.IsCustomerVisible
                    )

                    .OrderBy(
                        x => x.AttachmentType
                    )

                    .ThenByDescending(
                        x => x.CreatedAt
                    )

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.SoftwareVersionId,

                            x.FileName,

                            x.FileSize,

                            x.ContentType,

                            x.AttachmentType,

                            x.Remark,

                            x.CreatedAt
                        }
                    )

                    .ToListAsync();


            return Ok(
                attachments
            );
        }/// <summary>
         /// 客户下载版本附加资料。
         ///
         /// GET
         /// /api/my-software/attachments/{attachmentId}/download
         /// </summary>
        [HttpGet("attachments/{attachmentId:int}/download")]
        public async Task<IActionResult> DownloadAttachment(
            int attachmentId)
        {
            /*
             * 获取当前客户。
             */
            var customerId =
                await GetCurrentCustomerIdAsync();


            if (!customerId.HasValue)
            {
                return Forbid();
            }


            /*
             * 客户只能查询
             * IsCustomerVisible = true 的附件。
             */
            var attachment =
                await _dbContext
                    .SoftwareVersionAttachments

                    .AsNoTracking()

                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == attachmentId
                            &&
                            x.IsCustomerVisible
                    );


            if (attachment == null)
            {
                return NotFound(
                    "附件不存在"
                );
            }


            /*
             * 再检查附件所属版本。
             */
            var softwareVersion =
                await _dbContext.SoftwareVersions

                    .AsNoTracking()

                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                            attachment.SoftwareVersionId
                            &&
                            x.IsPublished
                            &&
                            x.VersionType == "Release"
                    );


            if (softwareVersion == null)
            {
                return Forbid();
            }


            /*
             * 再检查客户有没有这个软件授权。
             */
            var hasSoftwarePermission =
                await _dbContext.CustomerSoftwares

                    .AsNoTracking()

                    .AnyAsync(
                        x =>
                            x.CustomerId ==
                            customerId.Value
                            &&
                            x.SoftwareId ==
                            softwareVersion.SoftwareId
                            &&
                            x.IsEnabled
                            &&
                            x.Software != null
                            &&
                            x.Software.IsEnabled
                    );


            if (!hasSoftwarePermission)
            {
                return Forbid();
            }


            /*
             * 得到附件真实路径。
             */
            var physicalFilePath =
                Path.GetFullPath(
                    Path.Combine(
                        _environment.ContentRootPath,
                        attachment.StoragePath
                    )
                );


            var storageRoot =
                Path.GetFullPath(
                    Path.Combine(
                        _environment.ContentRootPath,
                        "Storage",
                        "VersionAttachments"
                    )
                );


            /*
             * 路径后面补目录分隔符。
             *
             * 避免类似：
             *
             * VersionAttachmentsXXX
             *
             * 被误判成合法目录。
             */
            var storageRootPrefix =
                storageRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                )
                +
                Path.DirectorySeparatorChar;


            if (!physicalFilePath.StartsWith(
                    storageRootPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(
                    "附件路径不合法"
                );
            }


            if (!System.IO.File.Exists(
                    physicalFilePath))
            {
                return NotFound(
                    "附件文件不存在"
                );
            }


            return PhysicalFile(
                physicalFilePath,

                string.IsNullOrWhiteSpace(
                    attachment.ContentType)
                    ? "application/octet-stream"
                    : attachment.ContentType,

                attachment.FileName,

                enableRangeProcessing: true
            );
        }
    }
}