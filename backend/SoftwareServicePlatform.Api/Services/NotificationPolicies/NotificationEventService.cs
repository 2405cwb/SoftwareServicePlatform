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
             * 1. 基础参数保护
             * ==========================================
             *
             * 通知属于辅助能力。
             * 即使通知参数异常，
             * 也不能让已经完成的核心业务反向失败。
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
             * 2. 读取数据库通知策略
             * ==========================================
             */
            NotificationPolicySnapshot? policy;

            try
            {
                policy =
                    await _policyService.GetAsync(
                        request.EventKey,
                        cancellationToken
                    );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "读取通知策略发生异常。EventKey={EventKey}",
                    request.EventKey
                );

                return;
            }


            if (policy == null)
            {
                return;
            }


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
             * 3. 解析真实接收人
             * ==========================================
             */
            IReadOnlyList<NotificationRecipient>
                recipients;

            try
            {
                recipients =
                    await _recipientResolver.ResolveAsync(
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
                    Array.Empty<NotificationRecipient>();
            }


            /*
             * ==========================================
             * 4. 最终通知级别和 Type
             * ==========================================
             */
            var level =
                string.IsNullOrWhiteSpace(
                    request.Level)
                    ? policy.DefaultLevel
                    : request.Level.Trim();


            var notificationType =
                string.IsNullOrWhiteSpace(
                    request.NotificationType)
                    ? request.EventKey
                    : request.NotificationType.Trim();


            /*
             * ==========================================
             * 5. 站内通知
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
                        var notification =
                            await _notificationService.AddAsync(
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
                        _logger.LogWarning(
                            ex,
                            "创建站内通知失败。EventKey={EventKey}, UserId={UserId}",
                            request.EventKey,
                            recipient.UserId
                        );
                    }
                }


                if (inAppCreatedCount > 0)
                {
                    var inAppSaved =
                        false;

                    try
                    {
                        await _dbContext.SaveChangesAsync(
                            cancellationToken
                        );

                        inAppSaved =
                            true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "保存站内通知失败。EventKey={EventKey}",
                            request.EventKey
                        );
                    }


                    /*
                     * 只有数据库保存成功，
                     * 才允许实时 SignalR 推送。
                     */
                    if (inAppSaved)
                    {
                        try
                        {
                            await _notificationService
                                .PushPendingAsync(
                                    cancellationToken
                                );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "SignalR 通知推送异常。EventKey={EventKey}",
                                request.EventKey
                            );
                        }
                    }
                }
            }


            /*
             * ==========================================
             * 6. 外部通知
             * ==========================================
             *
             * 外部通知必须在 InAppEnabled 判断之外。
             *
             * 因此允许：
             *
             * InAppEnabled = false
             * DingTalk     = true
             *
             * 只发钉钉，不生成站内通知。
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
                                channelPolicy.MentionAll,

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
                    _logger.LogWarning(
                        ex,
                        "外部通知发送异常。EventKey={EventKey}, Channel={Channel}",
                        request.EventKey,
                        channelPolicy.Channel
                    );
                }
            }


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