namespace SoftwareServicePlatform.Api.Dtos.Tickets
{
    /// <summary>
    /// 新增工单处理记录请求。
    ///
    /// 注意：
    /// CreatedByUserId 不允许前端传，
    /// 后端根据 JWT 自动获取。
    /// </summary>
    public class CreateTicketRecordRequest
    {
        /// <summary>
        /// 回复 / 处理内容。
        /// </summary>
        public string Content { get; set; } = string.Empty;


        /// <summary>
        /// 是否内部记录。
        ///
        /// false：
        /// 客户可以看到。
        ///
        /// true：
        /// 只有公司内部人员可以看到。
        ///
        /// Customer 用户不能创建内部记录。
        /// </summary>
        public bool IsInternal { get; set; } = false;
    }
}