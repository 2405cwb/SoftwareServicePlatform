namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 一次通知业务事件的输入参数。
    ///
    /// Controller / Service 以后不再关心：
    ///
    /// 谁需要收到通知
    /// 是否发送站内通知
    /// 是否发送钉钉
    /// 是否 @
    ///
    /// 这些全部由 NotificationPolicy 决定。
    ///
    /// 业务层只需要告诉通知系统：
    ///
    /// 1. 发生了什么事件
    /// 2. 对应哪个业务对象
    /// 3. 这次通知显示什么内容
    /// </summary>
    public class NotificationEventRequest
    {
        /// <summary>
        /// 通知事件编码。
        ///
        /// 不要手写字符串，优先使用：
        ///
        /// NotificationEventKeys.TicketCreated
        /// NotificationEventKeys.TicketTriaged
        /// ...
        /// </summary>
        public string EventKey { get; init; }
            = string.Empty;


        /// <summary>
        /// 通知业务上下文。
        ///
        /// Resolver 会根据这里面的：
        ///
        /// TicketId
        /// SoftwareVersionId
        ///
        /// 找到真正的通知接收用户。
        /// </summary>
        public NotificationRecipientContext Context
        {
            get;
            init;
        } = new();


        /// <summary>
        /// 通知标题。
        ///
        /// 例如：
        ///
        /// 工单分诊完成：TK202609180001
        /// </summary>
        public string Title { get; init; }
            = string.Empty;


        /// <summary>
        /// 通知正文。
        ///
        /// 第一版仍然由业务模块负责生成正文。
        ///
        /// 我们暂时不把消息模板放入数据库，
        /// 避免现在就引入复杂的模板引擎。
        /// </summary>
        public string Content { get; init; }
            = string.Empty;


        /// <summary>
        /// 可选的通知级别覆盖值。
        ///
        /// null：
        /// 使用 NotificationPolicy.DefaultLevel。
        ///
        /// 有值：
        /// 使用本次业务动态决定的级别。
        ///
        /// 例如：
        ///
        /// 普通工单：
        /// Info
        ///
        /// 紧急工单：
        /// Warning
        /// </summary>
        public string? Level { get; init; }


        /// <summary>
        /// 点击站内通知后跳转的位置。
        ///
        /// 例如：
        ///
        /// /tickets?ticketId=15
        /// </summary>
        public string? TargetUrl { get; init; }


        /// <summary>
        /// 通知去重 Key。
        ///
        /// 例如：
        ///
        /// ticket:15:created
        /// ticket:15:sla:resolution:overdue
        ///
        /// 同一个业务事件如果可能被重复执行，
        /// 应该提供相同的 DedupKey。
        ///
        /// Notification 表已经通过：
        ///
        /// UserId + DedupKey
        ///
        /// 防止同一用户收到重复站内通知。
        ///
        /// 如果不需要去重，可以为空。
        /// </summary>
        public string? DedupKey { get; init; }


        /// <summary>
        /// 可选的 Notification.Type。
        ///
        /// 如果没有填写，
        /// 默认直接使用 EventKey。
        ///
        /// 第一版保留这个字段，
        /// 方便逐步迁移现有旧通知类型。
        /// </summary>
        public string? NotificationType { get; init; }
    }
}