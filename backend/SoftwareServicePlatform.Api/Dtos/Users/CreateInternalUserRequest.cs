namespace SoftwareServicePlatform.Api.Dtos.Users
{
    /// <summary>
    /// 管理员创建公司内部用户时提交的数据。
    /// </summary>
    public class CreateInternalUserRequest
    {
        /// <summary>
        /// 登录用户名。
        /// </summary>
        public string Username { get; set; } = string.Empty;


        /// <summary>
        /// 初始登录密码。
        /// </summary>
        public string Password { get; set; } = string.Empty;


        /// <summary>
        /// 用户显示名称。
        /// 例如：张三、李工。
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;


        /// <summary>
        /// 用户角色。
        ///
        /// 当前内部用户允许：
        ///
        /// Admin
        /// Support
        /// Developer
        /// Sales
        /// </summary>
        public string Role { get; set; } = string.Empty;


        /// <summary>
        /// 邮箱。
        /// </summary>
        public string Email { get; set; } = string.Empty;


        /// <summary>
        /// 手机号。
        /// </summary>
        public string Phone { get; set; } = string.Empty;
    }
}