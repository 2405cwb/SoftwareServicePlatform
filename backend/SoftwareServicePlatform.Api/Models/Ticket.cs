namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 客户问题工单。
    ///
    /// Ticket 表示客户提出的一次问题、咨询、故障或需求。
    ///
    /// 注意：
    /// Ticket 不等于 Bug。
    ///
    /// 例如：
    /// 1. 不会使用软件       -> Ticket
    /// 2. 安装失败           -> Ticket
    /// 3. 配置问题           -> Ticket
    /// 4. 软件程序缺陷       -> Ticket，后续可能转为 Bug
    /// </summary>
    public class Ticket
    {
        /// <summary>
        /// 工单数据库主键。
        /// </summary>
        public int Id { get; set; }


        /// <summary>
        /// 对外显示的工单编号。
        ///
        /// 例如：
        ///
        /// TK202609090001
        ///
        /// 后面创建工单时由后端自动生成，
        /// 不让前端自己填写。
        /// </summary>
        public string TicketNo { get; set; } = string.Empty;


        /// <summary>
        /// 工单标题。
        ///
        /// 例如：
        ///
        /// 软件启动后提示数据库连接失败
        /// </summary>
        public string Title { get; set; } = string.Empty;


        /// <summary>
        /// 问题详细描述。
        ///
        /// 客户可以在这里描述：
        ///
        /// 出现什么问题
        /// 怎么操作出现的
        /// 当前软件版本
        /// 错误提示等
        /// </summary>
        public string Description { get; set; } = string.Empty;


        /// <summary>
        /// 工单状态。
        ///
        /// 当前第一版使用字符串，
        /// 和目前 User.Role 的设计方式保持一致。
        ///
        /// Pending
        /// Processing
        /// Resolved
        /// Closed
        /// </summary>
        public string Status { get; set; } = "Pending";


        /// <summary>
        /// 工单优先级。
        ///
        /// Low
        /// Normal
        /// High
        /// Urgent
        /// </summary>
        public string Priority { get; set; } = "Normal";


        // =====================================================
        // 客户
        // =====================================================

        /// <summary>
        /// 这个工单属于哪个客户。
        /// </summary>
        public int CustomerId { get; set; }


        /// <summary>
        /// 所属客户导航属性。
        /// </summary>
        public Customer Customer { get; set; } = null!;


        // =====================================================
        // 软件
        // =====================================================

        /// <summary>
        /// 这个工单对应哪个软件。
        ///
        /// 例如：
        /// 道路检测数据处理软件。
        /// </summary>
        public int SoftwareId { get; set; }


        /// <summary>
        /// 工单对应的软件。
        /// </summary>
        public Software Software { get; set; } = null!;


        // =====================================================
        // 创建人
        // =====================================================

        /// <summary>
        /// 谁创建了这个工单。
        ///
        /// 可以是：
        ///
        /// Customer
        /// Support
        /// Admin
        ///
        /// 后面由当前登录 JWT 获取，
        /// 不允许前端随便传一个用户ID。
        /// </summary>
        public int CreatedByUserId { get; set; }


        /// <summary>
        /// 工单创建人。
        /// </summary>
        public User CreatedByUser { get; set; } = null!;


        // =====================================================
        // 当前处理人
        // =====================================================

        /// <summary>
        /// 当前工单分配给谁处理。
        ///
        /// 为什么是 int?：
        ///
        /// 刚刚创建的工单可能还没有人受理，
        /// 所以允许 null。
        /// </summary>
        public int? AssignedToUserId { get; set; }


        /// <summary>
        /// 当前处理人。
        ///
        /// 未分配时为 null。
        /// </summary>
        public User? AssignedToUser { get; set; }


        // =====================================================
        // 时间
        // =====================================================

        /// <summary>
        /// 工单创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        /// <summary>
        /// 工单最后更新时间。
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


        /// <summary>
        /// 工单解决时间。
        ///
        /// 尚未解决时为 null。
        /// </summary>
        public DateTime? ResolvedAt { get; set; }


        /// <summary>
        /// 当前工单的全部处理记录。
        ///
        /// Ticket
        ///     1
        ///     ↓
        ///     N
        /// TicketRecord
        /// </summary>
        public List<TicketRecord> Records { get; set; } = new();
    }
}