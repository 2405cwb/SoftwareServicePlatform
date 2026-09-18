namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 通知接收人解析器。
    ///
    /// 负责把数据库中的：
    ///
    /// RecipientStrategy
    ///
    /// 例如：
    ///
    /// AssigneeOrSupport
    ///
    /// 转换成真正的系统用户列表。
    /// </summary>
    public interface INotificationRecipientResolver
    {
        /// <summary>
        /// 根据接收人策略和业务上下文，
        /// 解析最终应该收到通知的用户。
        ///
        /// 通知属于辅助能力，
        /// 所以解析失败时原则上返回空集合，
        /// 不应该让正常工单业务失败。
        /// </summary>
        Task<IReadOnlyList<NotificationRecipient>>
            ResolveAsync(
                string recipientStrategy,
                NotificationRecipientContext context,
                CancellationToken cancellationToken = default
            );
    }
}