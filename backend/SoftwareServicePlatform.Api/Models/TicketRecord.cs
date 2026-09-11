namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 工单处理记录。
    ///
    /// 一个 Ticket 可以拥有多条 TicketRecord。
    ///
    /// 用来记录：
    ///
    /// 客户追加说明
    /// 售后回复
    /// 开发排查记录
    /// 处理结果
    /// 系统状态变化等
    /// </summary>
    public class TicketRecord
    {
        /// <summary>
        /// 数据库主键。
        /// </summary>
        public int Id { get; set; }


        // =====================================================
        // 所属工单
        // =====================================================

        /// <summary>
        /// 当前记录属于哪个 Ticket。
        /// </summary>
        public int TicketId { get; set; }


        /// <summary>
        /// 所属工单导航属性。
        /// </summary>
        public Ticket Ticket { get; set; } = null!;


        // =====================================================
        // 操作人
        // =====================================================

        /// <summary>
        /// 谁创建了这条处理记录。
        ///
        /// 可以是：
        ///
        /// Customer
        /// Support
        /// Developer
        /// Admin
        ///
        /// 后面创建处理记录时，
        /// 这个值由 JWT 自动获取，
        /// 不允许前端自己指定。
        /// </summary>
        public int CreatedByUserId { get; set; }


        /// <summary>
        /// 创建这条记录的用户。
        /// </summary>
        public User CreatedByUser { get; set; } = null!;


        // =====================================================
        // 记录内容
        // =====================================================

        /// <summary>
        /// 记录类型。
        ///
        /// 当前第一版使用字符串。
        ///
        /// Comment
        ///     普通回复 / 处理说明
        ///
        /// Assign
        ///     工单分配
        ///
        /// Resolve
        ///     标记已解决
        ///
        /// Close
        ///     关闭工单
        ///
        /// Reopen
        ///     重新打开
        ///
        /// System
        ///     系统自动记录
        /// </summary>
        public string RecordType { get; set; } = "Comment";


        /// <summary>
        /// 处理记录正文。
        ///
        /// 例如：
        ///
        /// 已联系客户，确认问题可以稳定复现。
        /// </summary>
        public string Content { get; set; } = string.Empty;


        /// <summary>
        /// 是否属于内部记录。
        ///
        /// false：
        /// 客户也可以看到。
        ///
        /// true：
        /// 仅公司内部人员可以看到。
        ///
        /// 例如开发人员可以记录：
        ///
        /// “怀疑算法模块 XX 存在越界，
        /// 已转给王工排查”
        ///
        /// 这种信息可能不适合直接展示给客户。
        /// </summary>
        public bool IsInternal { get; set; } = false;


        // =====================================================
        // 时间
        // =====================================================

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}