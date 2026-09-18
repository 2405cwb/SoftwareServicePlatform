namespace SoftwareServicePlatform.Api.Services.ExternalNotifications
{
    /// <summary>
    /// 外部通知接收人。
    ///
    /// 这里描述的是：
    ///
    /// “系统里的哪个用户需要收到/被提醒”
    ///
    /// 而不是某个具体平台的账户信息。
    /// </summary>
    public class ExternalNotificationRecipient
    {
        /// <summary>
        /// 软件服务管理平台内部 User.Id。
        ///
        /// 例如：
        ///
        /// User.Id = 15
        ///
        /// 表示本次通知涉及系统用户 15。
        ///
        /// 后续 DingTalk Sender 可以根据这个 ID
        /// 查找到该用户绑定的钉钉账户。
        /// </summary>
        public int UserId { get; set; }


        /// <summary>
        /// 用户显示名称。
        ///
        /// 主要用于：
        ///
        /// 日志
        /// 消息展示
        /// 外部账户不存在时的降级显示
        ///
        /// 例如：
        ///
        /// 张三
        /// 李四
        /// </summary>
        public string DisplayName { get; set; }
            = string.Empty;


        /// <summary>
        /// 是否需要在外部平台中 @ 该用户。
        ///
        /// true：
        ///
        /// 钉钉 Sender 尝试 @ 对应钉钉账号。
        ///
        /// false：
        ///
        /// 只是普通通知对象，
        /// 不一定需要在群消息中 @。
        /// </summary>
        public bool Mention { get; set; }
            = true;
    }
}