namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 平台用户
    ///
    /// 既可以表示公司内部人员，
    /// 也可以表示客户侧登录用户。
    /// </summary>
    public class User
    {
        /// <summary>
        /// 用户主键
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 登录账号
        ///
        /// 例如：
        /// admin
        /// zhangsan
        /// whglj001
        ///
        /// 后续数据库中必须保证唯一。
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 用户真实姓名 / 显示名称
        ///
        /// 例如：
        /// 张三
        /// 系统管理员
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 密码哈希值
        ///
        /// 注意：
        /// 数据库绝对不能保存明文密码。
        ///
        /// 后面我们会学习 Password Hash。
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// 用户角色
        ///
        /// 当前学习阶段先使用字符串。
        ///
        /// 例如：
        /// Admin
        /// Support
        /// Developer
        /// Sales
        /// Customer
        /// </summary>
        public string Role { get; set; } = "Customer";

        /// <summary>
        /// 联系邮箱
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 联系电话
        /// </summary>
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// 用户是否启用
        ///
        /// false 后即使密码正确，
        /// 后面也不允许登录。
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 所属客户ID。
        ///
        /// 为什么是 int?？
        ///
        /// Customer 用户：
        /// CustomerId = 3
        ///
        /// 公司内部管理员：
        /// CustomerId = null
        /// </summary>
        public int? CustomerId { get; set; }

        /// <summary>
        /// 用户所属客户。
        ///
        /// 公司内部人员可能没有客户，
        /// 所以允许为 null。
        /// </summary>
        public Customer? Customer { get; set; }

        /// <summary>
        /// 最后一次登录时间。
        ///
        /// 从未登录过时为 null。
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}