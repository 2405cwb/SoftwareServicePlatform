namespace SoftwareServicePlatform.Api.Services.ExternalNotifications
{
    /// <summary>
    /// 外部通知渠道统一接口。
    ///
    /// 每一个外部通知平台，
    /// 都实现这个接口。
    ///
    /// 例如：
    ///
    /// DingTalkNotificationSender
    /// WeComNotificationSender
    /// FeishuNotificationSender
    ///
    /// 这样业务层不需要知道每个平台具体怎么发消息。
    /// </summary>
    public interface IExternalNotificationSender
    {
        /// <summary>
        /// 当前渠道名称。
        ///
        /// 例如：
        ///
        /// DingTalk
        /// WeCom
        /// Feishu
        /// </summary>
        string Channel { get; }


        /// <summary>
        /// 发送一条外部通知。
        /// </summary>
        /// <param name="message">
        /// 与具体平台无关的统一消息。
        /// </param>
        /// <param name="cancellationToken">
        /// HTTP 请求取消标记。
        /// </param>
        /// <returns>
        /// true：发送成功
        /// false：发送失败
        /// </returns>
        Task<bool> SendAsync(
            ExternalNotificationMessage message,
            CancellationToken cancellationToken = default
        );
    }
}