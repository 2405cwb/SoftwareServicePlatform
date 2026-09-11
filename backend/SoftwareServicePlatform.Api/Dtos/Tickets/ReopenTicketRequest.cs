namespace SoftwareServicePlatform.Api.Dtos.Tickets
{
    /// <summary>
    /// 重新打开工单请求。
    /// </summary>
    public class ReopenTicketRequest
    {
        /// <summary>
        /// 重新打开原因。
        ///
        /// 例如：
        /// 按照提供的方法处理后问题仍然存在。
        /// </summary>
        public string Content { get; set; } = string.Empty;
    }
}