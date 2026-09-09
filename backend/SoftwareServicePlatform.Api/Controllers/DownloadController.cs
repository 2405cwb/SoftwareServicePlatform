using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Services;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 软件安装包下载。
    ///
    /// 流程：
    ///
    /// 1. 使用 JWT 申请下载票据
    /// 2. 后端验证用户权限
    /// 3. 返回随机 ticket
    /// 4. 浏览器凭 ticket 进行原生文件下载
    ///
    /// 这样无需把 JWT 放到 URL 中。
    /// </summary>
    [ApiController]
    [Route("api/download")]
    public class DownloadController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        private readonly IWebHostEnvironment _environment;

        private readonly IMemoryCache _memoryCache;


        public DownloadController(
            AppDbContext dbContext,
            IWebHostEnvironment environment,
            IMemoryCache memoryCache)
        {
            _dbContext = dbContext;

            _environment = environment;

            _memoryCache = memoryCache;
        }


        /// <summary>
        /// 为当前客户申请一个短时下载票据。
        ///
        /// POST:
        ///
        /// /api/download/version/12/ticket
        ///
        /// 这个接口必须携带 JWT。
        /// </summary>
        [Authorize(Roles = "Customer")]
        [HttpPost("version/{versionId}/ticket")]
        public async Task<IActionResult> CreateDownloadTicket(
            int versionId)
        {
            /*
             * ========================================
             * 1. 获取当前登录用户ID
             * ========================================
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
             * ========================================
             * 2. 查询当前用户
             * ========================================
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
             * Customer 必须属于一个客户。
             */
            if (!currentUser.CustomerId.HasValue)
            {
                return Forbid();
            }


            /*
             * 客户不存在或者已经停用。
             */
            if (
                currentUser.Customer == null
                ||
                !currentUser.Customer.IsEnabled
            )
            {
                return Forbid();
            }


            /*
             * ========================================
             * 3. 查询版本 + 软件
             * ========================================
             */
            var softwareVersion =
                await _dbContext.SoftwareVersions

                    .Include(x => x.Software)

                    .FirstOrDefaultAsync(
                        x => x.Id == versionId
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
             * ========================================
             * 4. 检查软件和版本状态
             * ========================================
             */

            // 软件必须启用
            if (!softwareVersion.Software.IsEnabled)
            {
                return Forbid();
            }


            // 软件级下载开关
            if (!softwareVersion.Software.AllowDownload)
            {
                return Forbid();
            }


            // 版本级下载开关
            if (!softwareVersion.AllowDownload)
            {
                return Forbid();
            }


            /*
             * 客户门户只允许下载已经发布的 Release。
             */
            if (
                !softwareVersion.IsPublished
                ||
                softwareVersion.VersionType != "Release"
            )
            {
                return Forbid();
            }


            /*
             * ========================================
             * 5. 检查当前客户是否真正拥有这个软件
             * ========================================
             */
            var hasPermission =
                await _dbContext.CustomerSoftwares

                    .AnyAsync(
                        x =>
                            x.CustomerId
                                == currentUser.CustomerId.Value
                            &&
                            x.SoftwareId
                                == softwareVersion.SoftwareId
                            &&
                            x.IsEnabled
                    );


            if (!hasPermission)
            {
                return Forbid();
            }


            /*
             * ========================================
             * 6. 检查安装包记录
             * ========================================
             */
            if (string.IsNullOrWhiteSpace(
                    softwareVersion.PackageRelativePath))
            {
                return NotFound(
                    "当前版本尚未上传安装包"
                );
            }


            /*
             * 同时检查磁盘文件是否存在。
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


            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(
                    "安装包文件不存在，请联系管理员"
                );
            }


            /*
             * ========================================
             * 7. 生成随机下载票据
             * ========================================
             *
             * 使用 32 字节随机数据：
             *
             * 256 bit
             *
             * 然后转成 64 位十六进制字符串。
             */
            var ticket =
                Convert
                    .ToHexString(
                        RandomNumberGenerator
                            .GetBytes(32)
                    )
                    .ToLowerInvariant();


            /*
             * 缓存 Key 加一个前缀，
             * 防止以后其他缓存和它重名。
             */
            var cacheKey =
                $"download-ticket:{ticket}";


            /*
             * 票据保存：
             *
             * UserId
             * SoftwareVersionId
             */
            var ticketInfo =
                new DownloadTicketInfo
                {
                    UserId = userId,

                    SoftwareVersionId =
                        softwareVersion.Id
                };


            /*
             * 下载票据只允许存在5分钟。
             *
             * 5分钟后服务器自动删除。
             */
            _memoryCache.Set(
                cacheKey,
                ticketInfo,
                TimeSpan.FromMinutes(5)
            );


            /*
             * ========================================
             * 8. 返回下载地址
             * ========================================
             */
            return Ok(
                new
                {
                    ticket,

                    expiresInSeconds = 300,

                    downloadUrl =
                        $"/api/download/file?ticket={ticket}"
                }
            );
        }


        /// <summary>
        /// 根据短时 ticket 下载安装包。
        ///
        /// GET:
        ///
        /// /api/download/file?ticket=xxxxx
        ///
        /// 注意：
        ///
        /// 这个接口没有 JWT。
        ///
        /// 因为 ticket 本身就是一个随机、
        /// 短时、不可预测的下载凭证。
        /// </summary>
        [AllowAnonymous]
        [HttpGet("file")]
        public async Task<IActionResult> DownloadFile(
            [FromQuery] string ticket)
        {
            /*
             * ========================================
             * 1. ticket 基础检查
             * ========================================
             */
            if (string.IsNullOrWhiteSpace(ticket))
            {
                return BadRequest(
                    "缺少下载票据"
                );
            }


            var cacheKey =
                $"download-ticket:{ticket}";


            /*
             * ========================================
             * 2. 从服务器缓存读取 ticket
             * ========================================
             */
            if (
                !_memoryCache.TryGetValue(
                    cacheKey,
                    out DownloadTicketInfo? ticketInfo
                )
                ||
                ticketInfo == null
            )
            {
                return Unauthorized(
                    "下载链接已经失效，请重新获取"
                );
            }


            /*
             * ========================================
             * 3. 再次读取用户
             * ========================================
             *
             * 即使 ticket 还没过期，
             * 用户也可能刚刚被管理员停用。
             */
            var currentUser =
                await _dbContext.Users

                    .Include(x => x.Customer)

                    .FirstOrDefaultAsync(
                        x => x.Id == ticketInfo.UserId
                    );


            if (
                currentUser == null
                ||
                !currentUser.IsEnabled
            )
            {
                return Forbid();
            }


            if (
                !currentUser.CustomerId.HasValue
                ||
                currentUser.Customer == null
                ||
                !currentUser.Customer.IsEnabled
            )
            {
                return Forbid();
            }


            /*
             * ========================================
             * 4. 重新查询版本
             * ========================================
             */
            var softwareVersion =
                await _dbContext.SoftwareVersions

                    .Include(x => x.Software)

                    .FirstOrDefaultAsync(
                        x =>
                            x.Id
                            ==
                            ticketInfo.SoftwareVersionId
                    );


            if (
                softwareVersion == null
                ||
                softwareVersion.Software == null
            )
            {
                return NotFound(
                    "软件版本不存在"
                );
            }


            /*
             * ========================================
             * 5. 再次检查当前下载权限
             * ========================================
             */

            if (!softwareVersion.Software.IsEnabled)
            {
                return Forbid();
            }


            if (!softwareVersion.Software.AllowDownload)
            {
                return Forbid();
            }


            if (!softwareVersion.AllowDownload)
            {
                return Forbid();
            }


            if (
                !softwareVersion.IsPublished
                ||
                softwareVersion.VersionType != "Release"
            )
            {
                return Forbid();
            }


            /*
             * 软件授权可能在 ticket 生成之后
             * 被管理员取消。
             *
             * 所以这里再次检查。
             */
            var hasPermission =
                await _dbContext.CustomerSoftwares

                    .AnyAsync(
                        x =>
                            x.CustomerId
                                == currentUser.CustomerId.Value
                            &&
                            x.SoftwareId
                                == softwareVersion.SoftwareId
                            &&
                            x.IsEnabled
                    );


            if (!hasPermission)
            {
                return Forbid();
            }


            /*
             * ========================================
             * 6. 找到磁盘上的安装包
             * ========================================
             */
            if (string.IsNullOrWhiteSpace(
                    softwareVersion.PackageRelativePath))
            {
                return NotFound(
                    "当前版本没有安装包"
                );
            }


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


            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(
                    "安装包文件不存在"
                );
            }


            /*
             * 客户真正看到的文件名。
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
             * ========================================
             * 7. 原生流式下载
             * ========================================
             *
             * 文件不会整个读取到服务器内存，
             * 也不会让 React 先生成 Blob。
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