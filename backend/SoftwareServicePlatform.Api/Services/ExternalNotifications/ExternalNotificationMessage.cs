namespace SoftwareServicePlatform.Api.Services.ExternalNotifications
{
    /// <summary>
    /// 外部通知统一消息模型。
    ///
    /// 该模型不依赖具体外部平台。
    ///
    /// 无论最终发送到：
    ///
    /// 钉钉
    /// 企业微信
    /// 飞书
    /// 邮件
    ///
    /// 业务层都统一使用这个模型。
    /// </summary>
    public class ExternalNotificationMessage
    {
        /// <summary>
        /// 通知标题。
        ///
        /// 例如：
        ///
        /// 新紧急工单
        /// 工单已分配
        /// 软件版本发布
        /// SLA 即将超时
        /// </summary>
        public string Title { get; set; }
            = string.Empty;


        /// <summary>
        /// 通知正文。
        /// </summary>
        public string Content { get; set; }
            = string.Empty;


        /// <summary>
        /// 通知级别。
        ///
        /// 当前约定：
        ///
        /// Info
        /// Warning
        /// Error
        /// </summary>
        public string Level { get; set; }
            = "Info";


        /// <summary>
        /// 可选业务跳转地址。
        ///
        /// 例如：
        ///
        /// /tickets/123
        ///
        /// 后续如果外部平台支持链接，
        /// Sender 可以把它转换成可点击地址。
        /// </summary>
        public string? TargetUrl { get; set; }


        /// <summary>
        /// 本次通知涉及的用户。
        ///
        /// 注意：
        ///
        /// 这里存的是我们自己系统的 User，
        /// 而不是钉钉 UserId、企业微信 UserId。
        ///
        /// 具体平台 Sender 会负责把：
        ///
        /// 系统 UserId
        ///
        /// 转换成：
        ///
        /// DingTalk UserId
        /// WeCom UserId
        /// Feishu UserId
        ///
        /// 等平台身份。
        /// </summary>
        public List<ExternalNotificationRecipient>
            Recipients
        { get; set; } = new();


        /// <summary>
        /// 是否 @ 所有人。
        ///
        /// 一般情况下应该为 false。
        ///
        /// 只有真正非常重要的通知，
        /// 才考虑开启。
        /// </summary>
        public bool MentionAll { get; set; }
            = false;
    }
}