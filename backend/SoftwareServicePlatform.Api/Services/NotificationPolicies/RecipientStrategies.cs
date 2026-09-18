namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 通知接收人解析策略。
    ///
    /// NotificationPolicy 表中保存的是这里的策略名称，
    /// 而不是某个具体 UserId。
    ///
    /// 后面 RecipientResolver 会根据：
    ///
    /// 当前业务对象
    /// +
    /// RecipientStrategy
    ///
    /// 动态计算真正需要通知的用户。
    ///
    /// 例如：
    ///
    /// Assignee
    ///     ↓
    /// Ticket.AssignedToUserId
    ///
    /// Support
    ///     ↓
    /// 当前所有启用的售后用户
    /// </summary>
    public static class RecipientStrategies
    {
        /// <summary>
        /// 所有启用的售后人员。
        /// </summary>
        public const string Support =
            "Support";


        /// <summary>
        /// 当前工单处理人。
        /// </summary>
        public const string Assignee =
            "Assignee";


        /// <summary>
        /// 当前业务对应的客户用户。
        /// </summary>
        public const string Customer =
            "Customer";


        /// <summary>
        /// 优先通知当前处理人。
        ///
        /// 如果工单尚未分配处理人，
        /// 则通知所有售后。
        /// </summary>
        public const string AssigneeOrSupport =
            "AssigneeOrSupport";


        /// <summary>
        /// 当前处理人 + 所有售后。
        /// </summary>
        public const string AssigneeAndSupport =
            "AssigneeAndSupport";


        /// <summary>
        /// 当前处理人 + 售后 + 管理员。
        ///
        /// 主要用于严重事件，
        /// 例如 SLA 已经超时。
        /// </summary>
        public const string AssigneeSupportAdmin =
            "AssigneeSupportAdmin";


        /// <summary>
        /// 客户用户 + 当前处理人。
        /// </summary>
        public const string CustomerAndAssignee =
            "CustomerAndAssignee";


        /// <summary>
        /// 当前软件版本实际发布范围内的客户用户。
        ///
        /// 后续正式迁移 Version.Published
        /// 通知时使用。
        /// </summary>
        public const string VersionAudience =
            "VersionAudience";
    }
}