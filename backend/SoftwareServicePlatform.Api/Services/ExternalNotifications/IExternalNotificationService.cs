namespace SoftwareServicePlatform.Api.Services.ExternalNotifications
{
    /// <summary>
    /// 外部通知统一调度服务。
    ///
    /// Controller、NotificationService、工单业务等，
    /// 后面都应该依赖这个接口。
    ///
    /// 不要让业务代码直接依赖：
    ///
    /// DingTalkNotificationSender
    /// WeComNotificationSender
    /// FeishuNotificationSender
    ///
    /// 这样以后新增通知平台时，
    /// 业务代码不需要跟着修改。
    /// </summary>
    public interface IExternalNotificationService
    {
        /// <summary>
        /// 通过指定渠道发送一条外部通知。
        /// </summary>
        /// <param name="channel">
        /// 通知渠道。
        ///
        /// 例如：
        /// DingTalk
        /// WeCom
        /// Feishu
        /// </param>
        /// <param name="message">
        /// 统一的外部通知消息。
        /// </param>
        /// <param name="cancellationToken">
        /// 请求取消标记。
        /// </param>
        /// <returns>
        /// true：发送成功
        /// false：发送失败
        /// </returns>
        Task<bool> SendAsync(
            string channel,
            ExternalNotificationMessage message,
            CancellationToken cancellationToken = default
        );
    }
}