namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 一名最终解析出来的通知接收人。
    ///
    /// 这里只保存通知系统真正需要的
    /// 基础用户信息。
    ///
    /// 不直接返回 User Entity，
    /// 避免上层代码意外修改数据库实体。
    /// </summary>
    public class NotificationRecipient
    {
        /// <summary>
        /// 平台内部用户 ID。
        ///
        /// 后面：
        ///
        /// 站内通知
        ///     ↓
        /// Notification.UserId
        ///
        /// 钉钉 @
        ///     ↓
        /// ExternalUserBinding.UserId
        ///
        /// 都使用这个 ID。
        /// </summary>
        public int UserId { get; init; }


        /// <summary>
        /// 用户显示名称。
        ///
        /// 主要用于日志、
        /// 调试和后续外部通知显示。
        /// </summary>
        public string DisplayName { get; init; }
            = string.Empty;


        /// <summary>
        /// 当前用户角色。
        ///
        /// 例如：
        ///
        /// Support
        /// Developer
        /// Customer
        /// Admin
        /// </summary>
        public string Role { get; init; }
            = string.Empty;


        /// <summary>
        /// 所属客户 ID。
        ///
        /// 内部人员一般为 null。
        /// </summary>
        public int? CustomerId { get; init; }
    }
}