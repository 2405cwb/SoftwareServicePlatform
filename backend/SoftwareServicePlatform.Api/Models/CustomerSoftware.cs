namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 客户与软件之间的绑定关系。
    ///
    /// 一个客户可以拥有多个软件，
    /// 一个软件也可以被多个客户使用，
    /// 因此通过 CustomerSoftware 中间表建立多对多关系。
    /// </summary>
    public class CustomerSoftware
    {
        /// <summary>
        /// 主键
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 客户ID
        /// </summary>
        public int CustomerId { get; set; }

        /// <summary>
        /// 软件ID
        /// </summary>
        public int SoftwareId { get; set; }

        /// <summary>
        /// 当前客户是否仍然拥有该软件的使用权限
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 客户与软件建立绑定关系的时间
        /// </summary>
        public DateTime BoundAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 备注
        /// 例如合同编号、特殊授权说明等
        /// </summary>
        public string Remark { get; set; } = string.Empty;

        /// <summary>
        /// 客户导航属性
        /// </summary>
        public Customer? Customer { get; set; }

        /// <summary>
        /// 软件导航属性
        /// </summary>
        public Software? Software { get; set; }
    }
}