namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 客户端自动更新凭证。
    ///
    /// 一个 CustomerSoftware 只允许对应一套更新凭证。
    /// 数据库只保存 TokenHash，不保存明文 Token。
    /// </summary>
    public class ClientUpdateCredential
    {
        public int Id { get; set; }


        /// <summary>
        /// 客户 + 软件绑定关系。
        /// </summary>
        public int CustomerSoftwareId { get; set; }

        public CustomerSoftware? CustomerSoftware
        {
            get;
            set;
        }


        /// <summary>
        /// UpdateToken 的 SHA256。
        /// </summary>
        public string TokenHash { get; set; } =
            string.Empty;


        /// <summary>
        /// 后台展示使用的 Token 前缀。
        /// 不保存完整 Token。
        /// </summary>
        public string TokenPrefix { get; set; } =
            string.Empty;


        public bool IsEnabled { get; set; } = true;


        public DateTime CreatedAt { get; set; } =
            DateTime.UtcNow;


        public DateTime UpdatedAt { get; set; } =
            DateTime.UtcNow;


        public DateTime? LastUsedAt { get; set; }
    }
}