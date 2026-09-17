namespace SoftwareServicePlatform.Api.Services.ExternalNotifications
{
    /// <summary>
    /// 外部通知测试请求。
    ///
    /// 用于后台测试：
    ///
    /// 钉钉
    /// 企业微信
    /// 飞书
    ///
    /// 是否可以正常发送。
    /// </summary>
    public class ExternalNotificationTestRequest
    {
        /// <summary>
        /// 要测试的通知渠道。
        ///
        /// 例如：
        ///
        /// DingTalk
        /// WeCom
        /// Feishu
        /// </summary>
        public string Channel { get; set; }
            = string.Empty;
    }
}