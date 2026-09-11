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
        /// 客户编码。
        ///
        /// 例如：
        /// WH001
        ///
        /// 注册用户不直接填写数据库 CustomerId，
        /// 后端根据客户编码找到对应客户。
        /// </summary>
        public string CustomerCode { get; set; } = string.Empty;

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