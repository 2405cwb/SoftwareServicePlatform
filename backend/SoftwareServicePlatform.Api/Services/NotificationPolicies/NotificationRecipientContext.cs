namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 通知接收人解析上下文。
    ///
    /// RecipientStrategy 只描述：
    ///
    /// “通知谁”
    ///
    /// 例如：
    ///
    /// Assignee
    /// Support
    /// Customer
    ///
    /// 但是 Resolver 还需要知道：
    ///
    /// “是哪一张工单？”
    /// “是哪一个软件版本？”
    ///
    /// 所以使用这个 Context
    /// 把当前业务对象的 ID 传进来。
    ///
    /// 这里故意只保存 ID，
    /// 不直接传 EF Entity，
    /// 避免通知系统和 Controller
    /// 之间产生过强耦合。
    /// </summary>
    public class NotificationRecipientContext
    {
        /// <summary>
        /// 当前通知关联的工单 ID。
        ///
        /// 工单类事件使用。
        ///
        /// 例如：
        ///
        /// Ticket.Created
        /// Ticket.Triaged
        /// Ticket.CustomerReplied
        /// Ticket.SlaOverdue
        /// </summary>
        public int? TicketId { get; init; }


        /// <summary>
        /// 当前通知关联的软件版本 ID。
        ///
        /// Version.Published 使用。
        /// </summary>
        public int? SoftwareVersionId { get; init; }
    }
}