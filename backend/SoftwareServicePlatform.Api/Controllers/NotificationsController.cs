using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 当前登录用户的站内通知。
    ///
    /// 注意：
    /// 所有接口都只能操作“自己的通知”。
    /// 前端不能通过修改 NotificationId
    /// 查看或修改其他用户的通知。
    /// </summary>
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;


        public NotificationsController(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        /// <summary>
        /// 查询当前用户通知。
        ///
        /// GET:
        ///
        /// /api/notifications
        ///
        /// /api/notifications?page=1&pageSize=20
        ///
        /// /api/notifications?unreadOnly=true
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool unreadOnly = false)
        {
            /*
             * ==========================================
             * 1. 获取当前登录用户
             * ==========================================
             */

            if (!TryGetCurrentUserId(
                    out var currentUserId))
            {
                return Unauthorized(
                    "无法获取当前登录用户"
                );
            }


            /*
             * ==========================================
             * 2. 分页参数保护
             * ==========================================
             */

            page = Math.Max(
                page,
                1
            );


            pageSize = Math.Clamp(
                pageSize,
                1,
                100
            );


            /*
             * ==========================================
             * 3. 建立查询
             * ==========================================
             *
             * 最关键的权限条件：
             *
             * x.UserId == currentUserId
             *
             * 这意味着无论前端传什么参数，
             * 当前用户永远只能查询自己的通知。
             */

            var query =
                _dbContext.Notifications
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.UserId
                            ==
                            currentUserId
                    );


            /*
             * 只查看未读通知。
             */
            if (unreadOnly)
            {
                query =
                    query.Where(
                        x => !x.IsRead
                    );
            }


            /*
             * ==========================================
             * 4. 查询总数
             * ==========================================
             */

            var total =
                await query.CountAsync();


            /*
             * ==========================================
             * 5. 分页读取
             * ==========================================
             */

            var items =
                await query

                    .OrderByDescending(
                        x => x.CreatedAt
                    )

                    .ThenByDescending(
                        x => x.Id
                    )

                    .Skip(
                        (page - 1)
                        * pageSize
                    )

                    .Take(
                        pageSize
                    )

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.Type,

                            x.Level,

                            x.Title,

                            x.Content,

                            x.TargetUrl,

                            x.IsRead,

                            x.ReadAt,

                            x.CreatedAt
                        }
                    )

                    .ToListAsync();


            /*
             * ==========================================
             * 6. 返回分页结果
             * ==========================================
             */

            return Ok(
                new
                {
                    page,

                    pageSize,

                    total,

                    totalPages =
                        total == 0
                            ? 0
                            : (int)Math.Ceiling(
                                total
                                /
                                (double)pageSize
                            ),

                    items
                }
            );
        }


        /// <summary>
        /// 获取当前用户未读通知数量。
        ///
        /// GET:
        ///
        /// /api/notifications/unread-count
        ///
        /// 顶部铃铛：
        ///
        /// 🔔 5
        ///
        /// 就依赖这个接口。
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            if (!TryGetCurrentUserId(
                    out var currentUserId))
            {
                return Unauthorized(
                    "无法获取当前登录用户"
                );
            }


            var count =
                await _dbContext.Notifications

                    .AsNoTracking()

                    .CountAsync(
                        x =>
                            x.UserId
                            ==
                            currentUserId
                            &&
                            !x.IsRead
                    );


            return Ok(
                new
                {
                    count
                }
            );
        }


        /// <summary>
        /// 将指定通知标记为已读。
        ///
        /// PUT:
        ///
        /// /api/notifications/15/read
        /// </summary>
        [HttpPut("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(
            int id)
        {
            if (!TryGetCurrentUserId(
                    out var currentUserId))
            {
                return Unauthorized(
                    "无法获取当前登录用户"
                );
            }


            /*
             * 注意这里不能只写：
             *
             * x.Id == id
             *
             * 必须同时检查：
             *
             * x.UserId == currentUserId
             */

            var notification =
                await _dbContext.Notifications

                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == id
                            &&
                            x.UserId
                            ==
                            currentUserId
                    );


            /*
             * 即使这个 NotificationId
             * 实际属于其他用户，
             *
             * 我们也返回 NotFound。
             *
             * 不告诉攻击者：
             *
             * “这个通知存在，只是不是你的。”
             */
            if (notification == null)
            {
                return NotFound(
                    "通知不存在"
                );
            }


            /*
             * 已经读过，再调用一次也不会出问题。
             *
             * 这就是一种简单的幂等操作。
             */
            if (!notification.IsRead)
            {
                notification.IsRead =
                    true;


                notification.ReadAt =
                    DateTime.UtcNow;


                await _dbContext
                    .SaveChangesAsync();
            }


            return Ok(
                new
                {
                    message =
                        "通知已标记为已读"
                }
            );
        }


        /// <summary>
        /// 将当前用户全部通知标记为已读。
        ///
        /// PUT:
        ///
        /// /api/notifications/read-all
        /// </summary>
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            if (!TryGetCurrentUserId(
                    out var currentUserId))
            {
                return Unauthorized(
                    "无法获取当前登录用户"
                );
            }


            var notifications =
                await _dbContext.Notifications

                    .Where(
                        x =>
                            x.UserId
                            ==
                            currentUserId
                            &&
                            !x.IsRead
                    )

                    .ToListAsync();


            /*
             * 没有未读通知也不是错误。
             */
            if (notifications.Count == 0)
            {
                return Ok(
                    new
                    {
                        message =
                            "没有未读通知",

                        updatedCount = 0
                    }
                );
            }


            var now =
                DateTime.UtcNow;


            foreach (
                var notification
                in notifications)
            {
                notification.IsRead =
                    true;

                notification.ReadAt =
                    now;
            }


            await _dbContext
                .SaveChangesAsync();


            return Ok(
                new
                {
                    message =
                        "全部通知已标记为已读",

                    updatedCount =
                        notifications.Count
                }
            );
        }


        /// <summary>
        /// 从 JWT 中取得当前登录用户 ID。
        ///
        /// 登录时我们把 User.Id
        /// 写入了 ClaimTypes.NameIdentifier。
        /// </summary>
        private bool TryGetCurrentUserId(
            out int currentUserId)
        {
            var userIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );


            return int.TryParse(
                userIdText,
                out currentUserId
            );
        }
    }
}