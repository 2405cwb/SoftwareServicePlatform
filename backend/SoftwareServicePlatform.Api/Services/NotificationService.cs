using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Hubs;
using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services.ExternalNotifications;

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


        /// <summary>
        /// 外部通知统一调度服务。
        ///
        /// NotificationService 不直接知道钉钉怎么发送，
        /// 只通过这个统一接口调用。
        /// </summary>
        private readonly IExternalNotificationService
            _externalNotificationService;


        /// <summary>
        /// 等待数据库保存成功后再进行推送的通知。
        ///
        /// 以前这里只保存 Notification。
        ///
        /// 现在除了 Notification 本身，
        /// 还需要同时保存它的外部投递策略。
        /// </summary>
        private readonly List<PendingNotification>
            _pendingNotifications = new();

        /// <summary>
        /// AppDbContext 由 ASP.NET Core DI 容器自动注入。
        /// </summary>
        public NotificationService(
      AppDbContext dbContext,
      IHubContext<NotificationHub> hubContext,
      ILogger<NotificationService> logger,
      IExternalNotificationService externalNotificationService)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
            _logger = logger;
            _externalNotificationService = externalNotificationService;
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
     string? dedupKey = null,
     NotificationDeliveryOptions? deliveryOptions = null)
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
 * ==========================================
 * 保存等待推送的通知
 * ==========================================
 *
 * 注意：
 * 这里依旧没有真正发送 SignalR 或钉钉。
 *
 * 必须等业务代码：
 *
 * SaveChangesAsync()
 *
 * 成功以后，
 * 再调用 PushPendingAsync()。
 *
 * 这样可以确保：
 *
 * 数据库业务失败
 *     ↓
 * 不会提前发送一条假的外部通知。
 */
            _pendingNotifications.Add(
                new PendingNotification
                {
                    Notification = notification,

                    DeliveryOptions = deliveryOptions
                }
            );

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
             * 复制当前等待推送的数据，
             * 随后马上清空缓存。
             */
            var pendingNotifications =
                _pendingNotifications.ToList();

            _pendingNotifications.Clear();


            /*
             * ==========================================
             * 第一阶段：SignalR
             * ==========================================
             *
             * 每个站内 Notification
             * 单独推送给对应用户。
             */
            foreach (var pending in pendingNotifications)
            {
                var notification =
                    pending.Notification;

                try
                {
                    await _hubContext.Clients
                        .User(
                            notification.UserId.ToString()
                        )
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
                            cancellationToken
                        );
                }
                catch (Exception ex)
                {
                    /*
                     * SignalR 失败不能影响：
                     *
                     * 数据库通知
                     * 钉钉通知
                     */
                    _logger.LogWarning(
                        ex,
                        "SignalR 推送通知失败，NotificationId={NotificationId}",
                        notification.Id
                    );
                }
            }


            /*
             * ==========================================
             * 第二阶段：外部通知
             * ==========================================
             *
             * 注意：
             *
             * 这里必须在上面的 foreach 外面。
             */
            var externalPending =
                pendingNotifications
                    .Where(x =>
                        x.DeliveryOptions != null
                        &&
                        x.DeliveryOptions
                            .ExternalChannels.Count > 0
                    )
                    .ToList();


            /*
             * 没有任何外部通知，
             * 正常结束即可。
             */
            if (externalPending.Count == 0)
            {
                return;
            }


            /*
             * ==========================================
             * 按业务事件合并
             * ==========================================
             *
             * 例如新工单通知 3 个售后：
             *
             * 站内通知：
             * 3 条
             *
             * 钉钉：
             * 1 条
             */
            var groups =
                externalPending.GroupBy(
                    x =>
                        x.DeliveryOptions!
                            .ExternalEventKey
                        ??
                        x.Notification.DedupKey
                        ??
                        $"notification:{x.Notification.Id}"
                );


            foreach (var group in groups)
            {
                var first =
                    group.First();

                var firstNotification =
                    first.Notification;


                /*
                 * ==========================================
                 * 整理外部通知接收人
                 * ==========================================
                 *
                 * 现在先保存系统 UserId。
                 *
                 * 后面做 @ 功能的时候：
                 *
                 * UserId
                 * ↓
                 * ExternalUserBinding
                 * ↓
                 * 钉钉用户
                 */
                var recipients =
                    group
                        .GroupBy(
                            x =>
                                x.Notification.UserId
                        )
                        .Select(x =>
                        {
                            var item =
                                x.First();

                            return
                                new ExternalNotificationRecipient
                                {
                                    UserId =
                                        item.Notification.UserId,

                                    DisplayName =
                                        string.Empty,

                                    Mention =
                                        item.DeliveryOptions!
                                            .MentionRecipient
                                };
                        })
                        .ToList();


                var externalMessage =
                    new ExternalNotificationMessage
                    {
                        Title =
                            firstNotification.Title,

                        Content =
                            firstNotification.Content,

                        Level =
                            firstNotification.Level,

                        TargetUrl =
                            firstNotification.TargetUrl,

                        Recipients =
                            recipients,

                        MentionAll =
                            group.Any(
                                x =>
                                    x.DeliveryOptions!
                                        .MentionAll
                            )
                    };


                /*
                 * 一个业务事件可能同时：
                 *
                 * DingTalk
                 * WeCom
                 * Feishu
                 */
                var channels =
                    group
                        .SelectMany(
                            x =>
                                x.DeliveryOptions!
                                    .ExternalChannels
                        )
                        .Where(
                            x =>
                                !string.IsNullOrWhiteSpace(x)
                        )
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase
                        )
                        .ToList();


                foreach (var channel in channels)
                {
                    try
                    {
                        var success =
                            await _externalNotificationService
                                .SendAsync(
                                    channel,
                                    externalMessage,
                                    cancellationToken
                                );


                        if (!success)
                        {
                            _logger.LogWarning(
                                "外部通知发送失败。Channel={Channel}, EventKey={EventKey}",
                                channel,
                                group.Key
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        /*
                         * 外部服务失败绝不能反向破坏业务。
                         */
                        _logger.LogWarning(
                            ex,
                            "外部通知发送异常。Channel={Channel}, EventKey={EventKey}",
                            channel,
                            group.Key
                        );
                    }
                }
            }
        }
        /// <summary>
        /// 一条等待推送的通知。
        ///
        /// Notification：
        /// 数据库中的站内通知。
        ///
        /// DeliveryOptions：
        /// 描述是否还需要发送钉钉、企业微信等。
        /// </summary>
        private class PendingNotification
    {
        public Notification Notification { get; set; }
            = null!;

        public NotificationDeliveryOptions?
            DeliveryOptions
        { get; set; }
    }
    }
 
}