namespace SoftwareServicePlatform.Api.Services.ExternalNotifications
{
    /// <summary>
    /// 钉钉机器人配置。
    ///
    /// 对应：
    ///
    /// appsettings.Development.json
    ///
    /// "DingTalk": {
    ///     "Enabled": true,
    ///     "Webhook": "...",
    ///     "Keyword": "软件服务平台"
    /// }
    /// </summary>
    public class DingTalkOptions
    {
        /// <summary>
        /// 是否启用钉钉通知。
        ///
        /// false：
        /// 系统即使调用钉钉 Sender，
        /// 也不会真正发送 HTTP 请求。
        /// </summary>
        public bool Enabled { get; set; }


        /// <summary>
        /// 钉钉自定义机器人 Webhook。
        ///
        /// 这个地址本质上属于敏感凭证，
        /// 不能提交到 GitHub。
        /// </summary>
        public string Webhook { get; set; }
            = string.Empty;


        /// <summary>
        /// 自定义机器人安全关键词。
        ///
        /// 如果钉钉机器人启用了关键词校验，
        /// 消息正文必须包含这个关键词。
        /// </summary>
        public string Keyword { get; set; }
            = string.Empty;
    }
}