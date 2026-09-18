namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 通知策略。
    ///
    /// 一条 NotificationPolicy 描述一个“业务事件”
    /// 应该采用什么方式进行通知。
    ///
    /// 例如：
    ///
    /// Ticket.Created
    ///     ↓
    /// 客户创建新工单
    ///     ↓
    /// 站内通知：开启
    /// 接收人策略：Support
    ///     ↓
    /// 外部渠道：
    /// DingTalk：开启
    ///
    ///
    /// 注意：
    /// 这张表只负责“通知策略”，
    /// 不保存真正发送给用户的 Notification。
    ///
    /// Notification 表：
    ///     保存已经产生的实际通知。
    ///
    /// NotificationPolicy 表：
    ///     保存应该如何产生通知的规则。
    /// </summary>
    public class NotificationPolicy
    {
        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }


        /// <summary>
        /// 业务事件唯一编码。
        ///
        /// 例如：
        ///
        /// Ticket.Created
        /// Ticket.Triaged
        /// Ticket.Reassigned
        /// Ticket.CustomerReplied
        /// Ticket.SlaWarning
        /// Ticket.SlaOverdue
        /// Version.Published
        ///
        /// 业务代码以后通过 EventKey
        /// 查找对应通知策略。
        ///
        /// EventKey 一旦投入使用，
        /// 原则上不要随意修改。
        /// </summary>
        public string EventKey { get; set; }
            = string.Empty;


        /// <summary>
        /// 给管理员看的事件名称。
        ///
        /// 例如：
        ///
        /// 客户创建工单
        /// 工单完成分诊
        /// 客户追加回复
        /// SLA 已超时
        ///
        /// EventKey 给程序使用，
        /// EventName 给人看。
        /// </summary>
        public string EventName { get; set; }
            = string.Empty;


        /// <summary>
        /// 对该通知事件的说明。
        ///
        /// 后续做“通知策略管理”页面时，
        /// 可以直接显示给管理员。
        ///
        /// 例如：
        ///
        /// “客户从门户提交新工单后触发，
        /// 默认通知所有售后人员。”
        /// </summary>
        public string? Description { get; set; }


        /// <summary>
        /// 整个通知事件是否启用。
        ///
        /// false：
        /// 这个业务事件不产生任何通知。
        ///
        /// 注意：
        /// 它的优先级高于 InAppEnabled
        /// 和各个外部 Channel。
        /// </summary>
        public bool IsEnabled { get; set; }
            = true;


        /// <summary>
        /// 是否生成站内通知。
        ///
        /// true：
        /// 写入 Notifications 表，
        /// 并通过 SignalR 实时推送。
        ///
        /// false：
        /// 不产生站内通知，
        /// 但仍然可能发送钉钉等外部通知。
        /// </summary>
        public bool InAppEnabled { get; set; }
            = true;


        /// <summary>
        /// 接收人解析策略。
        ///
        /// 这里不直接保存 UserId。
        ///
        /// 因为不同工单对应不同的：
        ///
        /// 客户
        /// 当前处理人
        /// 售后人员
        ///
        /// 所以这里只描述“应该通知谁”。
        ///
        /// 第一版计划支持：
        ///
        /// Support
        /// Assignee
        /// Customer
        /// AssigneeOrSupport
        /// AssigneeAndSupport
        /// AssigneeSupportAdmin
        /// CustomerAndAssignee
        ///
        /// 真正查用户的工作，
        /// 后面由 RecipientResolver 完成。
        /// </summary>
        public string RecipientStrategy { get; set; }
            = string.Empty;


        /// <summary>
        /// 默认通知级别。
        ///
        /// 当前约定：
        ///
        /// Info
        /// Warning
        /// Danger
        ///
        /// 这里是默认值。
        ///
        /// 某些特殊业务场景仍然允许代码覆盖。
        ///
        /// 例如：
        /// 普通工单默认 Info，
        /// 但紧急工单可以动态升级为 Warning。
        /// </summary>
        public string DefaultLevel { get; set; }
            = "Info";


        /// <summary>
        /// 创建时间。
        /// 数据库存 UTC。
        /// </summary>
        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;


        /// <summary>
        /// 最后更新时间。
        ///
        /// 后续管理员修改通知策略时更新。
        /// </summary>
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;


        /// <summary>
        /// 当前业务事件配置的外部通知渠道。
        ///
        /// 例如：
        ///
        /// Ticket.Triaged
        ///     ├─ DingTalk
        ///     ├─ WeCom
        ///     └─ Feishu
        /// </summary>
        public ICollection<NotificationPolicyChannel>
            Channels
        { get; set; }
            = new List<NotificationPolicyChannel>();
    }
}