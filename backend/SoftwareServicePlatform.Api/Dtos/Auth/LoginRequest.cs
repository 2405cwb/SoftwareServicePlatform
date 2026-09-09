namespace SoftwareServicePlatform.Api.Dtos.Auth
{
    /// <summary>
    /// 登录请求
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// 登录账号
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 用户输入的明文密码
        ///
        /// 只用于本次登录验证，
        /// 不会保存到数据库。
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }
}