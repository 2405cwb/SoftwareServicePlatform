namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 系统统一通知事件调度服务。
    ///
    /// 所有业务模块以后发送通知，
    /// 原则上统一从这里进入。
    ///
    /// Controller 不应该再直接操作：
    ///
    /// NotificationService
    /// ExternalNotificationService
    /// DingTalkNotificationSender
    ///
    /// 而只需要发布一个业务通知事件。
    /// </summary>
    public interface INotificationEventService
    {
        /// <summary>
        /// 发布一次通知事件。
        ///
        /// 通知属于业务的辅助能力。
        ///
        /// 即使通知发送失败，
        /// 也不应该让：
        ///
        /// 工单创建
        /// 工单分诊
        /// 软件发布
        ///
        /// 等核心业务失败。
        /// </summary>
        Task PublishAsync(
            NotificationEventRequest request,
            CancellationToken cancellationToken = default
        );
    }
}