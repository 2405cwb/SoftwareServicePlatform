using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 系统用户与外部通知平台账号绑定管理。
    ///
    /// 当前第一版主要用于：
    ///
    /// 软件服务平台 User
    ///         ↓
    /// ExternalUserBinding
    ///         ↓
    /// DingTalk
    ///         ↓
    /// 钉钉手机号 / 钉钉用户ID
    ///
    /// 以后如果接入：
    ///
    /// 企业微信
    /// 飞书
    /// Email
    ///
    /// 仍然继续复用这一套结构。
    ///
    /// 当前属于后台管理能力，
    /// 只允许 Admin 使用。
    /// </summary>
    [ApiController]
    [Route("api/external-user-bindings")]
    [Authorize(Roles = "Admin")]
    public class ExternalUserBindingsController
        : ControllerBase
    {
        private readonly AppDbContext
            _dbContext;


        public ExternalUserBindingsController(
            AppDbContext dbContext)
        {
            _dbContext =
                dbContext;
        }


        /// <summary>
        /// 查询外部账号绑定。
        ///
        /// GET:
        ///
        /// /api/external-user-bindings
        ///
        /// 或：
        ///
        /// /api/external-user-bindings?channel=DingTalk
        ///
        ///
        /// 这个接口主要给后面的：
        ///
        /// 用户管理页面
        ///
        /// 使用。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBindings(
            [FromQuery] string? channel)
        {
            /*
             * ==========================================
             * 1. 创建查询
             * ==========================================
             *
             * 当前只是查看数据，
             * 所以使用 AsNoTracking。
             */
            var query =
                _dbContext.ExternalUserBindings
                    .AsNoTracking()
                    .Include(x => x.User)
                    .AsQueryable();


            /*
             * ==========================================
             * 2. 可选渠道过滤
             * ==========================================
             *
             * 例如：
             *
             * channel = DingTalk
             *
             * 前端只需要查看钉钉绑定。
             */
            if (!string.IsNullOrWhiteSpace(
                    channel))
            {
                var normalizedChannel =
                    channel.Trim();


                query =
                    query.Where(
                        x =>
                            x.Channel ==
                            normalizedChannel
                    );
            }


            /*
             * ==========================================
             * 3. 返回安全 DTO
             * ==========================================
             *
             * 不直接返回整个 User，
             * 防止 PasswordHash 等字段被序列化出去。
             */
            var bindings =
                await query
                    .OrderBy(x =>
                        x.User!.DisplayName
                    )
                    .Select(
                        x =>
                            new
                            {
                                x.Id,

                                x.UserId,

                                Username =
                                    x.User == null
                                        ? string.Empty
                                        : x.User.Username,

                                DisplayName =
                                    x.User == null
                                        ? string.Empty
                                        : x.User.DisplayName,

                                Role =
                                    x.User == null
                                        ? string.Empty
                                        : x.User.Role,

                                x.Channel,

                                x.ExternalUserId,

                                /*
                                 * 管理员需要查看当前绑定手机号，
                                 * 所以前端接口允许返回。
                                 *
                                 * 但注意：
                                 *
                                 * 后续任何日志中
                                 * 都不要打印完整手机号。
                                 */
                                x.Mobile,

                                x.IsEnabled,

                                x.CreatedAt,

                                x.UpdatedAt
                            }
                    )
                    .ToListAsync();


            return Ok(
                bindings
            );
        }


        /// <summary>
        /// 查询某个系统用户的外部绑定。
        ///
        /// GET:
        ///
        /// /api/external-user-bindings/user/15
        /// </summary>
        [HttpGet("user/{userId:int}")]
        public async Task<IActionResult> GetUserBindings(
            int userId)
        {
            /*
             * ==========================================
             * 1. 参数检查
             * ==========================================
             */
            if (userId <= 0)
            {
                return BadRequest(
                    "用户ID无效"
                );
            }


            /*
             * ==========================================
             * 2. 用户必须真实存在
             * ==========================================
             */
            var userExists =
                await _dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.Id == userId
                    );


            if (!userExists)
            {
                return NotFound(
                    "用户不存在"
                );
            }


            /*
             * ==========================================
             * 3. 查询该用户全部外部绑定
             * ==========================================
             */
            var bindings =
                await _dbContext
                    .ExternalUserBindings
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.UserId ==
                            userId
                    )
                    .OrderBy(
                        x =>
                            x.Channel
                    )
                    .Select(
                        x =>
                            new
                            {
                                x.Id,

                                x.UserId,

                                x.Channel,

                                x.ExternalUserId,

                                x.Mobile,

                                x.IsEnabled,

                                x.CreatedAt,

                                x.UpdatedAt
                            }
                    )
                    .ToListAsync();


            return Ok(
                bindings
            );
        }


        /// <summary>
        /// 新增或修改某个用户的外部平台绑定。
        ///
        /// PUT:
        ///
        /// /api/external-user-bindings/15/DingTalk
        ///
        ///
        /// 这是 Upsert：
        ///
        /// 不存在
        ///     ↓
        /// 创建
        ///
        /// 已存在
        ///     ↓
        /// 更新
        ///
        ///
        /// 这样前端不需要自己判断：
        ///
        /// POST 还是 PUT。
        /// </summary>
        [HttpPut("{userId:int}/{channel}")]
        public async Task<IActionResult> SaveBinding(
            int userId,
            string channel,
            SaveExternalUserBindingRequest request)
        {
            /*
             * ==========================================
             * 1. 用户ID检查
             * ==========================================
             */
            if (userId <= 0)
            {
                return BadRequest(
                    "用户ID无效"
                );
            }


            /*
             * ==========================================
             * 2. 渠道名称检查
             * ==========================================
             */
            if (string.IsNullOrWhiteSpace(
                    channel))
            {
                return BadRequest(
                    "外部通知渠道不能为空"
                );
            }


            /*
             * ==========================================
             * 3. 当前允许的外部平台
             * ==========================================
             *
             * 第一版目前只有：
             *
             * DingTalk
             *
             * 不允许前端随便提交：
             *
             * SMS
             * Telegram
             * XXX
             *
             * 因为这些 Sender 根本还没实现。
             */
            var allowedChannels =
                new[]
                {
                    "DingTalk"
                };


            var normalizedChannel =
                allowedChannels
                    .FirstOrDefault(
                        x =>
                            string.Equals(
                                x,
                                channel.Trim(),
                                StringComparison.OrdinalIgnoreCase
                            )
                    );


            if (normalizedChannel == null)
            {
                return BadRequest(
                    "当前不支持该外部通知渠道"
                );
            }


            /*
             * ==========================================
             * 4. 检查系统用户
             * ==========================================
             */
            var user =
                await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == userId
                    );


            if (user == null)
            {
                return NotFound(
                    "用户不存在"
                );
            }


            /*
             * ==========================================
             * 5. 字段长度保护
             * ==========================================
             *
             * 与 AppDbContext 中字段长度保持一致。
             */
            var externalUserId =
                string.IsNullOrWhiteSpace(
                    request.ExternalUserId)
                    ? null
                    : request.ExternalUserId.Trim();


            var mobile =
                string.IsNullOrWhiteSpace(
                    request.Mobile)
                    ? null
                    : request.Mobile.Trim();


            if (
                externalUserId != null
                &&
                externalUserId.Length > 200
            )
            {
                return BadRequest(
                    "外部用户ID不能超过200个字符"
                );
            }


            if (
                mobile != null
                &&
                mobile.Length > 50
            )
            {
                return BadRequest(
                    "手机号不能超过50个字符"
                );
            }


            /*
             * ==========================================
             * 6. DingTalk 第一版绑定规则
             * ==========================================
             *
             * 当前我们准备通过：
             *
             * Mobile
             *
             * 做机器人 @ 人。
             *
             * ExternalUserId 暂时保留，
             * 以后接通讯录 API 时再使用。
             *
             * 如果绑定处于启用状态，
             * 至少要存在一个可用外部身份。
             */
            if (
                request.IsEnabled
                &&
                string.IsNullOrWhiteSpace(
                    mobile)
                &&
                string.IsNullOrWhiteSpace(
                    externalUserId)
            )
            {
                return BadRequest(
                    "启用外部账号绑定时，请至少填写手机号或外部用户ID"
                );
            }


            /*
             * ==========================================
             * 7. 查询现有绑定
             * ==========================================
             *
             * 数据库已经有唯一索引：
             *
             * UserId + Channel
             *
             * 所以一个用户同一个渠道只允许一条。
             */
            var binding =
                await _dbContext
                    .ExternalUserBindings
                    .FirstOrDefaultAsync(
                        x =>
                            x.UserId == userId
                            &&
                            x.Channel ==
                            normalizedChannel
                    );


            var now =
                DateTime.UtcNow;


            /*
             * ==========================================
             * 8. 不存在：创建
             * ==========================================
             */
            if (binding == null)
            {
                binding =
                    new ExternalUserBinding
                    {
                        UserId =
                            userId,

                        Channel =
                            normalizedChannel,

                        ExternalUserId =
                            externalUserId,

                        Mobile =
                            mobile,

                        IsEnabled =
                            request.IsEnabled,

                        CreatedAt =
                            now,

                        UpdatedAt =
                            now
                    };


                _dbContext
                    .ExternalUserBindings
                    .Add(
                        binding
                    );
            }
            else
            {
                /*
                 * ==========================================
                 * 9. 已存在：更新
                 * ==========================================
                 */
                binding.ExternalUserId =
                    externalUserId;

                binding.Mobile =
                    mobile;

                binding.IsEnabled =
                    request.IsEnabled;

                binding.UpdatedAt =
                    now;
            }


            /*
             * ==========================================
             * 10. 保存数据库
             * ==========================================
             */
            await _dbContext
                .SaveChangesAsync();


            /*
             * ==========================================
             * 11. 返回
             * ==========================================
             */
            return Ok(
                new
                {
                    message =
                        "外部账号绑定保存成功",

                    binding.Id,

                    binding.UserId,

                    binding.Channel,

                    binding.ExternalUserId,

                    binding.Mobile,

                    binding.IsEnabled,

                    binding.UpdatedAt
                }
            );
        }
    }


    /// <summary>
    /// 保存外部用户绑定请求。
    /// </summary>
    public class SaveExternalUserBindingRequest
    {
        /// <summary>
        /// 外部平台 UserId。
        ///
        /// 当前 DingTalk 第一版可以不填写。
        /// </summary>
        public string? ExternalUserId
        {
            get;
            set;
        }


        /// <summary>
        /// 外部平台绑定手机号。
        ///
        /// 当前主要用于 DingTalk @ 用户。
        /// </summary>
        public string? Mobile
        {
            get;
            set;
        }


        /// <summary>
        /// 当前绑定是否启用。
        /// </summary>
        public bool IsEnabled
        {
            get;
            set;
        } = true;
    }
}