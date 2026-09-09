namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 客户信息
    /// </summary>
    public class Customer
    {
        /// <summary>
        /// 客户ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 客户名称
        /// 例如：武汉XX检测有限公司
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 客户内部编码
        /// 例如：WH001
        /// 后续建议保证唯一
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 客户类型
        /// 例如：企业、政府单位、高校、经销商
        /// </summary>
        public string CustomerType { get; set; } = string.Empty;

        /// <summary>
        /// 所属行业
        /// 例如：公路检测、交通工程、科研院所
        /// </summary>
        public string Industry { get; set; } = string.Empty;

        /// <summary>
        /// 所在省份
        /// </summary>
        public string Province { get; set; } = string.Empty;

        /// <summary>
        /// 所在城市
        /// </summary>
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// 详细地址
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// 主要联系人
        /// </summary>
        public string ContactName { get; set; } = string.Empty;

        /// <summary>
        /// 联系电话
        /// </summary>
        public string ContactPhone { get; set; } = string.Empty;

        /// <summary>
        /// 联系邮箱
        /// </summary>
        public string ContactEmail { get; set; } = string.Empty;

        /// <summary>
        /// 商务负责人
        /// 当前阶段先保存姓名，
        /// 后续用户系统完成后可以关联 User
        /// </summary>
        public string SalesOwner { get; set; } = string.Empty;

        /// <summary>
        /// 售后负责人
        /// </summary>
        public string SupportOwner { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用
        /// false 表示该客户已停止合作或暂时停用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
 
        /// 当前客户拥有的软件绑定关系
        /// </summary>
        public List<CustomerSoftware> CustomerSoftwares { get; set; } = new();
 
        /// 当前客户下面的登录用户
        ///
        /// 例如：
        /// 武汉公路局
        /// ├─ 张三
        /// ├─ 李四
        /// └─ 王五
        /// </summary>
        public List<User> Users { get; set; } = new();
 
    }
}