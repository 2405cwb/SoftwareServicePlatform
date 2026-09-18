using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Services.ExternalNotifications;

namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 系统统一通知事件调度器。
    ///
    /// 整个通知系统的核心流程：
    ///
    /// EventKey
    ///     ↓
    /// NotificationPolicyService
    ///     ↓
    /// 找到通知策略
    ///     ↓
    /// NotificationRecipientResolver
    ///     ↓
    /// 找到真正的接收用户
    ///     ↓
    /// ┌─────────────────────────────┐
    /// │                             │
    /// ↓                             ↓
    /// 站内通知                  外部通知
    /// NotificationService      ExternalNotificationService
    /// ↓                             ↓
    /// DB + SignalR             DingTalk / WeCom / Feishu
    ///
    ///
    /// 最重要的设计原则：
    ///
    /// 业务层负责：
    /// “发生了什么”
    ///
    /// 通知策略负责：
    /// “应该怎么通知”
    ///
    /// Sender 负责：
    /// “具体怎么发送”
    /// </summary>
    public class NotificationEventService
        : INotificationEventService
    {
        private readonly AppDbContext
            _dbContext;


        private readonly INotificationPolicyService
            _policyService;


        private readonly INotificationRecipientResolver
            _recipientResolver;


        private readonly INotificationService
            _notificationService;


        private readonly IExternalNotificationService
            _externalNotificationService;


        private readonly ILogger<NotificationEventService>
            _logger;


        public NotificationEventService(
            AppDbContext dbContext,
            INotificationPolicyService policyService,
            INotificationRecipientResolver recipientResolver,
            INotificationService notificationService,
            IExternalNotificationService externalNotificationService,
            ILogger<NotificationEventService> logger)
        {
            _dbContext =
                dbContext;

            _policyService =
                policyService;

            _recipientResolver =
                recipientResolver;

            _notificationService =
                notificationService;

            _externalNotificationService =
                externalNotificationService;

            _logger =
                logger;
        }


        /// <summary>
        /// 发布一次通知事件。
        /// </summary>
        public async Task PublishAsync(
            NotificationEventRequest request,
            CancellationToken cancellationToken = default)
        {
            /*
             * ==========================================
             * 1. 基础参数检查
             * ==========================================
             */

            if (request == null)
            {
                _logger.LogWarning(
                    "通知事件发布失败：Request 为空"
                );

                return;
            }


            if (string.IsNullOrWhiteSpace(
                    request.EventKey))
            {
                _logger.LogWarning(
                    "通知事件发布失败：EventKey 为空"
                );

                return;
            }


            if (string.IsNullOrWhiteSpace(
                    request.Title))
            {
                _logger.LogWarning(
                    "通知事件发布失败：Title 为空。EventKey={EventKey}",
                    request.EventKey
                );

                return;
            }


            if (string.IsNullOrWhiteSpace(
                    request.Content))
            {
                _logger.LogWarning(
                    "通知事件发布失败：Content 为空。EventKey={EventKey}",
                    request.EventKey
                );

                return;
            }


            /*
             * ==========================================
             * 2. 读取通知策略
             * ==========================================
             *
             * 例如：
             *
             * Ticket.Triaged
             *
             * 数据库可能配置为：
             *
             * IsEnabled = true
             * InAppEnabled = true
             * RecipientStrategy = Assignee
             *
             * DingTalk = true
             * MentionRecipient = true
             */

            NotificationPolicySnapshot? policy;

            try
            {
                policy =
                    await _policyService
                        .GetAsync(
                            request.EventKey,
                            cancellationToken
                        );
            }
            catch (Exception ex)
            {
                /*
                 * 通知系统异常不能让核心业务失败。
                 */
                _logger.LogError(
                    ex,
                    "读取通知策略发生异常。EventKey={EventKey}",
                    request.EventKey
                );

                return;
            }


            /*
             * 没有对应策略：
             *
             * 记录日志后跳过。
             */
            if (policy == null)
            {
                return;
            }


            /*
             * ==========================================
             * 3. 整个事件被关闭
             * ==========================================
             *
             * 这是最高级别总开关。
             *
             * IsEnabled = false
             *
             * 意味着：
             *
             * 不站内通知
             * 不 SignalR
             * 不 DingTalk
             * 不 WeCom
             * 不 Email
             */

            if (!policy.IsEnabled)
            {
                _logger.LogInformation(
                    "通知事件已被策略关闭。EventKey={EventKey}",
                    request.EventKey
                );

                return;
            }


            /*
             * ==========================================
             * 4. 解析真正的通知接收人
             * ==========================================
             */

            IReadOnlyList<NotificationRecipient>
                recipients;

            try
            {
                recipients =
                    await _recipientResolver
                        .ResolveAsync(
                            policy.RecipientStrategy,
                            request.Context,
                            cancellationToken
                        );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "解析通知接收人发生异常。EventKey={EventKey}, RecipientStrategy={RecipientStrategy}",
                    request.EventKey,
                    policy.RecipientStrategy
                );

                recipients =
                    Array.Empty<
                        NotificationRecipient>();
            }


            /*
             * ==========================================
             * 5. 确定最终通知等级
             * ==========================================
             *
             * 业务传了 Level：
             *
             * 使用业务动态值。
             *
             * 没传：
             *
             * 使用策略表 DefaultLevel。
             */

            var level =
                string.IsNullOrWhiteSpace(
                    request.Level)
                    ? policy.DefaultLevel
                    : request.Level.Trim();


            /*
             * Notification.Type
             *
             * 新代码可以直接使用 EventKey。
             *
             * 老代码迁移期间，
             * 也允许临时保留原来的 Type。
             */

            var notificationType =
                string.IsNullOrWhiteSpace(
                    request.NotificationType)
                    ? request.EventKey
                    : request.NotificationType.Trim();


            /*
             * ==========================================
             * 6. 生成站内通知
             * ==========================================
             */

            var inAppCreatedCount =
                0;


            if (policy.InAppEnabled)
            {
                foreach (var recipient in recipients)
                {
                    try
                    {
                        /*
                         * 每一个接收用户都拥有自己
                         * 独立的一条 Notification。
                         *
                         * Notification 表本来就是
                         * 按用户保存的。
                         */

                        var notification =
                            await _notificationService
                                .AddAsync(
                                    userId:
                                        recipient.UserId,

                                    type:
                                        notificationType,

                                    title:
                                        request.Title,

                                    content:
                                        request.Content,

                                    level:
                                        level,

                                    targetUrl:
                                        request.TargetUrl,

                                    /*
                                     * 同一个 DedupKey 可以给多个用户使用。
                                     *
                                     * 因为数据库唯一索引是：
                                     *
                                     * UserId + DedupKey
                                     *
                                     * 所以不需要再自己拼 UserId。
                                     */
                                    dedupKey:
                                        request.DedupKey
                                );


                        if (notification != null)
                        {
                            inAppCreatedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        /*
                         * 某一个用户通知生成失败，
                         * 不应该阻止其他用户。
                         */
                        _logger.LogWarning(
                            ex,
                            "创建站内通知失败。EventKey={EventKey}, UserId={UserId}",
                            request.EventKey,
                            recipient.UserId
                        );
                    }
                }


                /*
                 * ==========================================
                 * 7. 保存站内通知
                 * ==========================================
                 *
                 * NotificationService.AddAsync()
                 * 只是把 Notification 加入 DbContext，
                 * 不负责 SaveChanges。
                 */

                if (inAppCreatedCount > 0)
                {
                    try
                    {
                        await _dbContext
                            .SaveChangesAsync(
                                cancellationToken
                            );
                    }
                    catch (Exception ex)
                    {
                        /*
                         * 业务数据理论上已经在调用
                         * PublishAsync() 以前保存成功。
                         *
                         * 所以这里通知保存失败，
                         * 不能反向把核心业务变成失败。
                         */

                        _logger.LogError(
                            ex,
                            "保存站内通知失败。EventKey={EventKey}",
                            request.EventKey
                        );
                    }


                    /*
                     * ==========================================
                     * 8. SignalR 实时推送
                     * ==========================================
                     */

                    try
                    {
                        await _notificationService
                            .PushPendingAsync(
                                cancellationToken
                            );
                    }
                    catch (Exception ex)
                    {
                        /*
                         * SignalR 失败仍然不能影响业务。
                         *
                         * 数据库中已经保存的通知，
                         * 用户刷新页面以后仍然可以看到。
                         */

                        _logger.LogWarning(
                            ex,
                            "SignalR 通知推送异常。EventKey={EventKey}",
                            request.EventKey
                        );
                    }
                }
            }


            /*
             * ==========================================
             * 9. 发送外部通知
             * ==========================================
             *
             * 站内通知和外部通知彼此独立。
             *
             * 即使：
             *
             * InAppEnabled = false
             *
             * 仍然允许：
             *
             * DingTalkEnabled = true
             */

            var enabledChannels =
                policy.Channels
                    .Where(x =>
                        x.IsEnabled
                        &&
                        !string.IsNullOrWhiteSpace(
                            x.Channel
                        )
                    )
                    .ToList();


            foreach (var channelPolicy
                     in enabledChannels)
            {
                try
                {
                    /*
                     * 每个渠道单独创建 Message。
                     *
                     * 原因：
                     *
                     * DingTalk 可能要求 @ 接收人，
                     * Email 可能不需要。
                     *
                     * 所以 MentionRecipient
                     * 属于“渠道级策略”。
                     */

                    var externalMessage =
                        new ExternalNotificationMessage
                        {
                            Title =
                                request.Title,

                            Content =
                                request.Content,

                            Level =
                                level,

                            TargetUrl =
                                request.TargetUrl,

                            MentionAll =
                                channelPolicy
                                    .MentionAll,

                            /*
                             * 这里仍然保存的是
                             * 软件服务平台内部 UserId。
                             *
                             * 后面的具体 Sender：
                             *
                             * DingTalkNotificationSender
                             *
                             * 会通过 ExternalUserBinding
                             * 把 UserId 映射成钉钉账号。
                             */
                            Recipients =
                                recipients
                                    .Select(
                                        x =>
                                            new ExternalNotificationRecipient
                                            {
                                                UserId =
                                                    x.UserId,

                                                DisplayName =
                                                    x.DisplayName,

                                                Mention =
                                                    channelPolicy
                                                        .MentionRecipient
                                            }
                                    )
                                    .ToList()
                        };


                    var success =
                        await _externalNotificationService
                            .SendAsync(
                                channelPolicy.Channel,
                                externalMessage,
                                cancellationToken
                            );


                    if (!success)
                    {
                        _logger.LogWarning(
                            "外部通知发送失败。EventKey={EventKey}, Channel={Channel}",
                            request.EventKey,
                            channelPolicy.Channel
                        );
                    }
                }
                catch (Exception ex)
                {
                    /*
                     * 一个外部渠道失败，
                     * 不能影响：
                     *
                     * 站内通知
                     * 其他渠道
                     * 核心业务
                     */

                    _logger.LogWarning(
                        ex,
                        "外部通知发送异常。EventKey={EventKey}, Channel={Channel}",
                        request.EventKey,
                        channelPolicy.Channel
                    );
                }
            }


            /*
             * ==========================================
             * 10. 调度完成
             * ==========================================
             */

            _logger.LogInformation(
                "通知事件处理完成。EventKey={EventKey}, Recipients={RecipientCount}, InAppCreated={InAppCreatedCount}, ExternalChannels={ExternalChannelCount}",
                request.EventKey,
                recipients.Count,
                inAppCreatedCount,
                enabledChannels.Count
            );
        }
    }
}