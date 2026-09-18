namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 通知策略读取服务。
    ///
    /// 业务代码不应该自己直接查询：
    ///
    /// NotificationPolicies
    /// NotificationPolicyChannels
    ///
    /// 所有通知策略读取统一经过这里。
    /// </summary>
    public interface INotificationPolicyService
    {
        /// <summary>
        /// 根据业务事件编码读取通知策略。
        ///
        /// 例如：
        ///
        /// GetAsync(
        ///     NotificationEventKeys.TicketTriaged
        /// )
        ///
        /// 如果数据库不存在对应策略，
        /// 返回 null。
        ///
        /// 注意：
        /// 策略不存在时不要让正常业务失败。
        /// 后面的通知调度层会记录日志并跳过通知。
        /// </summary>
        Task<NotificationPolicySnapshot?>
            GetAsync(
                string eventKey,
                CancellationToken cancellationToken = default
            );
    }
}