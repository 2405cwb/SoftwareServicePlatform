using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Hubs;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Services
{
    /// <summary>
    /// 站内通知业务服务。
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHubContext<NotificationHub> _hubContext;

        private readonly ILogger<NotificationService> _logger;

        private readonly List<Notification> _pendingNotifications = new();

        /// <summary>
        /// AppDbContext 由 ASP.NET Core DI 容器自动注入。
        /// </summary>
        public NotificationService(
      AppDbContext dbContext,
      IHubContext<NotificationHub> hubContext,
      ILogger<NotificationService> logger)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// 添加一条站内通知。
        /// </summary>
        public async Task<Notification?> AddAsync(
            int userId,
            string type,
            string title,
            string content,
            string level = "Info",
            string? targetUrl = null,
            string? dedupKey = null)
        {
            /*
             * ==========================================
             * 1. 基础参数保护
             * ==========================================
             */

            if (userId <= 0)
            {
                throw new ArgumentException(
                    "通知接收用户ID无效",
                    nameof(userId)
                );
            }


            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException(
                    "通知标题不能为空",
                    nameof(title)
                );
            }


            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException(
                    "通知内容不能为空",
                    nameof(content)
                );
            }


            /*
             * ==========================================
             * 2. 处理 DedupKey
             * ==========================================
             *
             * 普通通知：
             *
             * dedupKey == null
             *
             * 不进行去重。
             *
             *
             * SLA 等后台提醒：
             *
             * dedupKey != null
             *
             * 需要保证同一业务事件只生成一次。
             */
            if (!string.IsNullOrWhiteSpace(dedupKey))
            {
                dedupKey = dedupKey.Trim();


                /*
                 * 先检查 DbContext 当前内存中
                 * 是否已经添加过相同通知。
                 *
                 * Local 表示：
                 *
                 * 当前 DbContext 正在跟踪，
                 * 但可能还没有写入数据库的数据。
                 */
                var existsInLocal =
                    _dbContext.Notifications.Local.Any(
                        x =>
                            x.UserId == userId
                            &&
                            x.DedupKey == dedupKey
                    );


                if (existsInLocal)
                {
                    return null;
                }


                /*
                 * 再检查数据库。
                 */
                var existsInDatabase =
                    await _dbContext.Notifications
                        .AsNoTracking()
                        .AnyAsync(
                            x =>
                                x.UserId == userId
                                &&
                                x.DedupKey == dedupKey
                        );


                if (existsInDatabase)
                {
                    return null;
                }
            }


            /*
             * ==========================================
             * 3. 创建 Notification
             * ==========================================
             */

            var notification =
                new Notification
                {
                    UserId = userId,

                    Type = string.IsNullOrWhiteSpace(type)
                        ? "General"
                        : type.Trim(),

                    Level = string.IsNullOrWhiteSpace(level)
                        ? "Info"
                        : level.Trim(),

                    Title = title.Trim(),

                    Content = content.Trim(),

                    TargetUrl =
                        string.IsNullOrWhiteSpace(targetUrl)
                            ? null
                            : targetUrl.Trim(),

                    DedupKey = dedupKey,

                    IsRead = false,

                    ReadAt = null,

                    CreatedAt = DateTime.UtcNow
                };


            _dbContext.Notifications.Add(notification);

            /*
             * 先记下来。
             * 等数据库 SaveChanges 成功以后再通过 SignalR 推送。
             */
            _pendingNotifications.Add(notification);

            return notification;
        }

        public async Task PushPendingAsync(
    CancellationToken cancellationToken = default)
        {
            if (_pendingNotifications.Count == 0)
            {
                return;
            }

            /*
             * 先复制并清空。
             *
             * SignalR 推送失败不能影响已经成功写入数据库的通知。
             * 即使实时推送失败，前端后续仍然可以通过 HTTP 查询到。
             */
            var notifications =
                _pendingNotifications.ToList();

            _pendingNotifications.Clear();


            foreach (var notification in notifications)
            {
                try
                {
                    await _hubContext.Clients
                        .User(notification.UserId.ToString())
                        .SendAsync(
                            "NotificationCreated",
                            new
                            {
                                notification.Id,
                                notification.Type,
                                notification.Level,
                                notification.Title,
                                notification.Content,
                                notification.TargetUrl,
                                notification.IsRead,
                                notification.ReadAt,
                                notification.CreatedAt
                            },
                            cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "SignalR 推送通知失败，NotificationId={NotificationId}",
                        notification.Id);
                }
            }
        }
    }
}