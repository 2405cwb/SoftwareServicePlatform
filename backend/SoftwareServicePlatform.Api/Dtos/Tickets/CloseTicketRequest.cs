namespace SoftwareServicePlatform.Api.Dtos.Tickets
{
    /// <summary>
    /// 关闭工单请求。
    ///
    /// 当前主要用于客户确认问题已经解决，
    /// 正式结束该工单。
    /// </summary>
    public class CloseTicketRequest
    {
        /// <summary>
        /// 关闭说明。
        ///
        /// 例如：
        /// 已确认问题解决，可以关闭工单。
        ///
        /// 可以不填写。
        /// </summary>
        public string Content { get; set; } = string.Empty;
    }
}