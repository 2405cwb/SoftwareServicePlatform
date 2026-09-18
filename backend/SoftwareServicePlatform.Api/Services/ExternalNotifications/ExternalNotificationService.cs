namespace SoftwareServicePlatform.Api.Services.ExternalNotifications
{
    /// <summary>
    /// 外部通知统一调度服务。
    ///
    /// 这个类本身不负责真正发送 HTTP 请求。
    ///
    /// 它只负责：
    ///
    /// Channel
    ///    ↓
    /// 找到对应 Sender
    ///    ↓
    /// 调用 Sender.SendAsync()
    ///
    ///
    /// 例如：
    ///
    /// DingTalk
    ///    ↓
    /// DingTalkNotificationSender
    ///
    ///
    /// WeCom
    ///    ↓
    /// WeComNotificationSender
    /// </summary>
    public class ExternalNotificationService
        : IExternalNotificationService
    {
        /// <summary>
        /// 当前系统中注册的所有外部通知渠道。
        ///
        /// ASP.NET Core DI 会自动把所有：
        ///
        /// IExternalNotificationSender
        ///
        /// 的实现都放到这个集合中。
        ///
        /// 以后可能包含：
        ///
        /// DingTalkNotificationSender
        /// WeComNotificationSender
        /// FeishuNotificationSender
        /// </summary>
        private readonly IEnumerable<IExternalNotificationSender>
            _senders;


        /// <summary>
        /// 日志服务。
        /// </summary>
        private readonly ILogger<ExternalNotificationService>
            _logger;


        public ExternalNotificationService(
            IEnumerable<IExternalNotificationSender> senders,
            ILogger<ExternalNotificationService> logger)
        {
            _senders = senders;

            _logger = logger;
        }


        /// <summary>
        /// 根据渠道名称发送通知。
        /// </summary>
        public async Task<bool> SendAsync(
            string channel,
            ExternalNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            /*
             * ==========================================
             * 1. 检查渠道名称
             * ==========================================
             */
            if (string.IsNullOrWhiteSpace(channel))
            {
                _logger.LogWarning(
                    "发送外部通知失败：没有指定通知渠道"
                );

                return false;
            }


            /*
             * ==========================================
             * 2. 从所有 Sender 中寻找对应渠道
             * ==========================================
             *
             * 忽略大小写：
             *
             * DingTalk
             * dingtalk
             * DINGTALK
             *
             * 都认为是同一个渠道。
             */
            var sender =
                _senders.FirstOrDefault(
                    x =>
                        string.Equals(
                            x.Channel,
                            channel,
                            StringComparison.OrdinalIgnoreCase
                        )
                );


            /*
             * ==========================================
             * 3. 没有找到对应渠道
             * ==========================================
             *
             * 例如用户传了：
             *
             * channel = "Telegram"
             *
             * 但当前系统没有 Telegram Sender。
             */
            if (sender == null)
            {
                _logger.LogWarning(
                    "没有找到外部通知渠道。Channel={Channel}",
                    channel
                );

                return false;
            }


            try
            {
                /*
                 * ==========================================
                 * 4. 调用真正的平台 Sender
                 * ==========================================
                 *
                 * 当前如果 Channel = DingTalk，
                 *
                 * 最终就会执行：
                 *
                 * DingTalkNotificationSender.SendAsync()
                 */
                return await sender.SendAsync(
                    message,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                /*
                 * ==========================================
                 * 5. 最后一层异常保护
                 * ==========================================
                 *
                 * 正常情况下，
                 * 每个平台自己的 Sender
                 * 就应该处理自己的异常。
                 *
                 * 这里再兜一层，
                 * 防止某个 Sender 编写不完善时，
                 * 外部通知异常继续向业务层传播。
                 */
                _logger.LogError(
                    ex,
                    "外部通知发送异常。Channel={Channel}",
                    channel
                );

                return false;
            }
        }
    }
}