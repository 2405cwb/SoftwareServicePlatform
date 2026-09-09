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

        public MySoftwareController(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
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
    }
}