namespace SoftwareServicePlatform.Api.Dtos.Tickets
{
    /// <summary>
    /// 解决工单请求。
    /// </summary>
    public class ResolveTicketRequest
    {
        /// <summary>
        /// 问题解决说明。
        ///
        /// 例如：
        /// 已确认是配置文件路径错误，
        /// 修改配置后软件可以正常启动。
        ///
        /// 解决说明默认属于客户可见内容。
        /// </summary>
        public string Content { get; set; } = string.Empty;
    }
}