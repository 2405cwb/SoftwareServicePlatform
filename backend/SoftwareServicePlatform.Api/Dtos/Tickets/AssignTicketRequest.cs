namespace SoftwareServicePlatform.Api.Dtos.Tickets
{
    /// <summary>
    /// 分配工单请求。
    ///
    /// Admin / Support 可以把工单
    /// 分配给售后人员或开发人员。
    /// </summary>
    public class AssignTicketRequest
    {
        /// <summary>
        /// 要把工单分配给哪个用户。
        /// </summary>
        public int AssignedToUserId { get; set; }
    }
}