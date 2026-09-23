namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 客户与软件之间的绑定关系。
    /// CustomerSoftware 负责“公司有没有这款软件”；
    /// ClientInstallation 负责“哪些设备允许自动更新”。
    /// </summary>
    public class CustomerSoftware
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int SoftwareId { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime BoundAt { get; set; } = DateTime.UtcNow;
        public string Remark { get; set; } = string.Empty;

        /// <summary>
        /// 允许同时保持启用状态的更新设备数量；0 = 不限制。
        /// </summary>
        public int MaxDeviceCount { get; set; } = 0;

        public Customer? Customer { get; set; }
        public Software? Software { get; set; }

        public ICollection<ClientInstallation> ClientInstallations { get; set; }
            = new List<ClientInstallation>();

        public ICollection<ClientActivationCode> ClientActivationCodes { get; set; }
            = new List<ClientActivationCode>();
    }
}
