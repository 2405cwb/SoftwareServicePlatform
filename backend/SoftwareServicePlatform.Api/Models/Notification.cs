namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 站内通知。
    ///
    /// 一条 Notification 表示：
    /// 某个用户需要看到的一条系统消息。
    ///
    /// 例如：
    /// - 工单被分配给你
    /// - 客户回复了工单
    /// - 工单即将 SLA 超时
    /// - 软件发布了新版本
    /// </summary>
    public class Notification
    {
        /// <summary>
        /// 通知主键。
        /// </summary>
        public int Id { get; set; }


        /// <summary>
        /// 这条通知属于哪个用户。
        ///
        /// 注意：
        /// 通知是“按用户”保存的，
        /// 不是一条通知所有人共享。
        /// </summary>
        public int UserId { get; set; }


        /// <summary>
        /// 通知接收用户。
        /// EF Core 导航属性。
        /// </summary>
        public User User { get; set; } = null!;


        /// <summary>
        /// 通知业务类型。
        ///
        /// 后面我们会使用：
        ///
        /// TicketCreated
        /// TicketAssigned
        /// TicketReply
        /// TicketResolved
        /// TicketClosed
        /// TicketReopened
        /// SlaWarning
        /// SlaOverdue
        /// VersionPublished
        ///
        /// 暂时使用 string，
        /// 和你目前 Role / Ticket Status 的设计保持一致。
        /// </summary>
        public string Type { get; set; } = "General";


        /// <summary>
        /// 通知级别。
        ///
        /// Info
        /// Warning
        /// Danger
        ///
        /// 前端后面可以根据级别显示不同图标/样式。
        /// </summary>
        public string Level { get; set; } = "Info";


        /// <summary>
        /// 通知标题。
        ///
        /// 例如：
        /// 工单已分配给你
        /// </summary>
        public string Title { get; set; } = string.Empty;


        /// <summary>
        /// 通知详细内容。
        ///
        /// 例如：
        /// 工单 TK202609150001 已分配给你，请及时处理。
        /// </summary>
        public string Content { get; set; } = string.Empty;


        /// <summary>
        /// 点击通知以后跳转到哪里。
        ///
        /// 例如：
        ///
        /// /tickets?ticketId=15
        ///
        /// 后面前端收到通知后，
        /// navigate(TargetUrl) 即可。
        /// </summary>
        public string? TargetUrl { get; set; }


        /// <summary>
        /// 通知去重标识。
        ///
        /// 主要用于后台定时任务。
        ///
        /// 例如：
        ///
        /// ticket:15:sla:resolution:overdue
        ///
        /// 后台服务可能每5分钟检查一次，
        /// 如果没有这个字段，
        /// 同一个 SLA 超时可能产生几十条通知。
        /// </summary>
        public string? DedupKey { get; set; }


        /// <summary>
        /// 是否已经阅读。
        /// </summary>
        public bool IsRead { get; set; } = false;


        /// <summary>
        /// 阅读时间。
        ///
        /// 未读时为 null。
        /// </summary>
        public DateTime? ReadAt { get; set; }


        /// <summary>
        /// 通知创建时间。
        /// 数据库存 UTC。
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}