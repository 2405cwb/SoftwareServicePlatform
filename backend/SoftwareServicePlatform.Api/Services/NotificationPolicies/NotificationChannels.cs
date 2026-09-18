namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 外部通知渠道统一名称。
    ///
    /// 避免系统中到处出现：
    ///
    /// "DingTalk"
    /// "WeCom"
    /// "Feishu"
    ///
    /// 这样的魔法字符串。
    /// </summary>
    public static class NotificationChannels
    {
        public const string DingTalk =
            "DingTalk";

        public const string WeCom =
            "WeCom";

        public const string Feishu =
            "Feishu";

        public const string Email =
            "Email";
    }
}