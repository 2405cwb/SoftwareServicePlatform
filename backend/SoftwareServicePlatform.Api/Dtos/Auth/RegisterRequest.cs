namespace SoftwareServicePlatform.Api.Dtos.Auth
{
    /// <summary>
    /// 用户注册请求
    ///
    /// 这个类只负责接收前端传过来的数据，
    /// 不直接对应数据库表。
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>
        /// 登录账号
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 明文密码。
        ///
        /// 注意：
        /// 这个字段不会直接存入数据库。
        /// 后端会先 Hash。
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 用户显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 所属客户ID
        ///
        /// 当前注册接口先用于创建客户用户，
        /// 所以必须指定客户。
        /// </summary>
        public int CustomerId { get; set; }

        /// <summary>
        /// 邮箱
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 手机号
        /// </summary>
        public string Phone { get; set; } = string.Empty;
    }
}