namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 通知策略的外部通知渠道配置。
    ///
    /// 一条 NotificationPolicy
    /// 可以拥有多个 Channel。
    ///
    /// 例如：
    ///
    /// Ticket.SlaOverdue
    ///
    ///     ├─ DingTalk：启用
    ///     ├─ WeCom：启用
    ///     └─ Email：关闭
    ///
    /// 这样以后增加新的通知平台，
    /// 不需要继续给 NotificationPolicy
    /// 增加：
    ///
    /// DingTalkEnabled
    /// WeComEnabled
    /// FeishuEnabled
    ///
    /// 等越来越多的字段。
    /// </summary>
    public class NotificationPolicyChannel
    {
        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }


        /// <summary>
        /// 所属通知策略 ID。
        /// </summary>
        public int NotificationPolicyId { get; set; }


        /// <summary>
        /// 所属通知策略。
        /// EF Core 导航属性。
        /// </summary>
        public NotificationPolicy NotificationPolicy
        {
            get;
            set;
        } = null!;


        /// <summary>
        /// 外部通知渠道名称。
        ///
        /// 当前：
        ///
        /// DingTalk
        ///
        /// 后续可以增加：
        ///
        /// WeCom
        /// Feishu
        /// Email
        /// </summary>
        public string Channel { get; set; }
            = string.Empty;


        /// <summary>
        /// 当前渠道是否启用。
        ///
        /// 例如：
        ///
        /// Ticket.Created
        ///
        /// DingTalk：
        /// IsEnabled = true
        ///
        /// WeCom：
        /// IsEnabled = false
        ///
        /// 就表示新工单当前只发钉钉。
        /// </summary>
        public bool IsEnabled { get; set; }
            = true;


        /// <summary>
        /// 是否尝试 @ 实际通知接收人。
        ///
        /// 例如：
        ///
        /// 工单分配给张三
        ///     ↓
        /// RecipientResolver 得到 UserId = 5
        ///     ↓
        /// ExternalUserBinding
        ///     ↓
        /// 找到张三对应的钉钉账号
        ///     ↓
        /// @张三
        /// </summary>
        public bool MentionRecipient { get; set; }
            = false;


        /// <summary>
        /// 是否 @ 所有人。
        ///
        /// 一般应该保持 false。
        ///
        /// 只有非常特殊的重要事件，
        /// 才考虑开启。
        /// </summary>
        public bool MentionAll { get; set; }
            = false;


        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;


        /// <summary>
        /// 最后更新时间。
        /// </summary>
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;
    }
}