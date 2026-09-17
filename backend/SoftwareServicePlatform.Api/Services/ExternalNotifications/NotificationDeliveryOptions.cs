namespace SoftwareServicePlatform.Api.Services.ExternalNotifications
{
    /// <summary>
    /// 通知投递策略。
    ///
    /// 用来描述一条站内通知除了：
    ///
    /// 数据库
    /// SignalR
    ///
    /// 之外，是否还需要投递到外部平台。
    ///
    /// 注意：
    /// 这里不包含任何钉钉专属字段，
    /// 所以后面企业微信、飞书也可以直接复用。
    /// </summary>
    public class NotificationDeliveryOptions
    {
        /// <summary>
        /// 需要投递的外部渠道。
        ///
        /// 例如：
        ///
        /// DingTalk
        /// WeCom
        /// Feishu
        ///
        /// 如果集合为空，
        /// 表示只发送站内通知，不发送外部通知。
        /// </summary>
        public List<string> ExternalChannels { get; set; }
            = new();


        /// <summary>
        /// 是否尝试在外部平台中 @ 当前通知接收用户。
        ///
        /// 例如：
        ///
        /// 工单分配给张三
        ///     ↓
        /// 站内通知张三
        ///     ↓
        /// 钉钉群消息 @张三
        ///
        /// 当前阶段钉钉 Sender 还没有真正实现 @，
        /// 但我们先把这个意图保存下来。
        /// </summary>
        public bool MentionRecipient { get; set; }
            = false;


        /// <summary>
        /// 是否 @ 所有人。
        ///
        /// 一般不要使用，
        /// 只给非常紧急的通知使用。
        /// </summary>
        public bool MentionAll { get; set; }
            = false;


        /// <summary>
        /// 外部通知事件唯一标识。
        ///
        /// 主要用于把同一业务事件产生的
        /// 多条站内通知合并成一条外部通知。
        ///
        /// 例如一个版本发布给 20 个用户：
        ///
        /// 站内：
        /// 20 条 Notification
        ///
        /// 钉钉：
        /// 只应该发 1 条消息
        ///
        /// 可以设置：
        ///
        /// version:15:published
        ///
        /// 这样 PushPendingAsync 会把这些通知归为同一组。
        ///
        /// 如果不填写，
        /// 后面会优先使用 Notification.DedupKey。
        /// </summary>
        public string? ExternalEventKey { get; set; }
    }
}