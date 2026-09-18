namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 系统通知事件统一编码。
    ///
    /// 以后业务代码不要再直接写：
    ///
    /// "Ticket.Created"
    /// "Ticket.Triaged"
    ///
    /// 而应该统一使用这里的常量。
    ///
    /// 这样可以避免：
    ///
    /// Ticket.Created
    /// TicketCreated
    /// ticket.created
    ///
    /// 这种字符串写法不统一的问题。
    ///
    /// 注意：
    /// EventKey 一旦投入生产并写入数据库，
    /// 原则上不要随意修改。
    /// </summary>
    public static class NotificationEventKeys
    {
        /// <summary>
        /// 客户创建新工单。
        /// </summary>
        public const string TicketCreated =
            "Ticket.Created";


        /// <summary>
        /// 售后完成工单分诊。
        /// </summary>
        public const string TicketTriaged =
            "Ticket.Triaged";


  

        /// <summary>
        /// 工单第一次分配给处理人。
        ///
        /// 与 Ticket.Reassigned 区分：
        ///
        /// Ticket.Assigned
        ///     原来没有处理人。
        ///
        /// Ticket.Reassigned
        ///     原来已经有处理人，
        ///     后续转交给另外一个人。
        /// </summary>
        public const string TicketAssigned =
            "Ticket.Assigned";


        /// <summary>
        /// 工单被重新分配给其他处理人。
        /// </summary>
        public const string TicketReassigned =
            "Ticket.Reassigned";


        /// <summary>
        /// 客户对工单追加公开回复。
        /// </summary>
        public const string TicketCustomerReplied =
            "Ticket.CustomerReplied";


        /// <summary>
        /// 公司内部人员对客户进行公开回复。
        ///
        /// 注意：
        /// 内部备注不属于这个事件。
        /// </summary>
        public const string TicketStaffReplied =
            "Ticket.StaffReplied";

        /// <summary>
        /// 已解决或关闭的工单被重新打开。
        /// </summary>
        public const string TicketReopened =
            "Ticket.Reopened";


        /// <summary>
        /// 工单 SLA 即将超时。
        /// </summary>
        public const string TicketSlaWarning =
            "Ticket.SlaWarning";


        /// <summary>
        /// 工单 SLA 已经超时。
        /// </summary>
        public const string TicketSlaOverdue =
            "Ticket.SlaOverdue";


        /// <summary>
        /// 工单被标记为已解决。
        /// </summary>
        public const string TicketResolved =
            "Ticket.Resolved";


        /// <summary>
        /// 工单正式关闭。
        /// </summary>
        public const string TicketClosed =
            "Ticket.Closed";


        /// <summary>
        /// 软件版本正式发布。
        /// </summary>
        public const string VersionPublished =
            "Version.Published";
    }
}