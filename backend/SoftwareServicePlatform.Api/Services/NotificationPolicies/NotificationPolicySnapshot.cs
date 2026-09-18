namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 通知策略读取结果。
    ///
    /// 为什么不直接把数据库里的
    /// NotificationPolicy Entity 返回给业务层？
    ///
    /// 因为 NotificationPolicy 是 EF Core 数据实体，
    /// 它属于数据库层。
    ///
    /// 后面的业务代码真正关心的只是：
    ///
    /// 是否启用
    /// 是否发送站内通知
    /// 接收人策略
    /// 默认通知级别
    /// 哪些外部渠道开启
    ///
    /// 所以这里转换成一个只读“策略快照”。
    ///
    /// 好处：
    ///
    /// 1. 业务层不会意外修改 EF Entity
    /// 2. 后面做缓存更方便
    /// 3. 数据库表结构改变时，对业务层影响更小
    /// </summary>
    public class NotificationPolicySnapshot
    {
        /// <summary>
        /// 业务事件唯一编码。
        ///
        /// 例如：
        ///
        /// Ticket.Created
        /// Ticket.Triaged
        /// </summary>
        public string EventKey { get; init; }
            = string.Empty;


        /// <summary>
        /// 给管理员看的事件名称。
        /// </summary>
        public string EventName { get; init; }
            = string.Empty;


        /// <summary>
        /// 整个事件是否启用。
        ///
        /// false 时：
        /// 所有通知渠道都应该停止。
        /// </summary>
        public bool IsEnabled { get; init; }


        /// <summary>
        /// 是否生成站内通知。
        /// </summary>
        public bool InAppEnabled { get; init; }


        /// <summary>
        /// 接收人解析策略。
        ///
        /// 例如：
        ///
        /// Support
        /// Assignee
        /// AssigneeOrSupport
        /// Customer
        /// </summary>
        public string RecipientStrategy { get; init; }
            = string.Empty;


        /// <summary>
        /// 默认通知等级。
        ///
        /// Info
        /// Warning
        /// Danger
        /// </summary>
        public string DefaultLevel { get; init; }
            = "Info";


        /// <summary>
        /// 当前事件对应的所有外部渠道策略。
        ///
        /// 包括已启用和未启用的配置。
        /// </summary>
        public IReadOnlyList<
            NotificationPolicyChannelSnapshot>
            Channels
        { get; init; }
            = Array.Empty<
                NotificationPolicyChannelSnapshot>();
    }


    /// <summary>
    /// 单个外部通知渠道策略快照。
    /// </summary>
    public class NotificationPolicyChannelSnapshot
    {
        /// <summary>
        /// 渠道名称。
        ///
        /// 例如：
        ///
        /// DingTalk
        /// WeCom
        /// Feishu
        /// </summary>
        public string Channel { get; init; }
            = string.Empty;


        /// <summary>
        /// 当前渠道是否启用。
        /// </summary>
        public bool IsEnabled { get; init; }


        /// <summary>
        /// 是否尝试 @ 通知接收人。
        /// </summary>
        public bool MentionRecipient { get; init; }


        /// <summary>
        /// 是否 @ 所有人。
        /// </summary>
        public bool MentionAll { get; init; }
    }
}