namespace SoftwareServicePlatform.Api.Dtos.Tickets
{
    /// <summary>
    /// 客户创建工单时提交的数据。
    ///
    /// 注意：
    ///
    /// 这里故意没有：
    ///
    /// CustomerId
    /// CreatedByUserId
    ///
    /// 因为这两个值必须由后端根据 JWT 自动确定，
    /// 不能相信前端传过来的用户身份。
    /// </summary>
    public class CreateTicketRequest
    {
        /// <summary>
        /// 出问题的软件ID。
        /// </summary>
        public int SoftwareId { get; set; }


        /// <summary>
        /// 工单标题。
        ///
        /// 例如：
        /// 软件启动后提示数据库连接失败
        /// </summary>
        public string Title { get; set; } = string.Empty;


        /// <summary>
        /// 问题详细描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;


        /// <summary>
        /// 优先级。
        ///
        /// Low
        /// Normal
        /// High
        /// Urgent
        /// </summary>
        public string Priority { get; set; } = "Normal";
    }
}