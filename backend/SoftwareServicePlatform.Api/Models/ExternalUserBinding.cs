namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 系统用户与外部通知平台账号之间的绑定关系。
    ///
    /// 例如：
    ///
    /// 软件服务平台 User.Id = 12
    ///             ↓
    /// ExternalUserBinding
    ///             ↓
    /// DingTalk
    ///             ↓
    /// 对应的钉钉手机号 / 钉钉用户ID
    ///
    /// 这样工单业务代码永远只需要认识我们自己的 UserId，
    /// 不需要知道钉钉、企业微信等平台的账号格式。
    /// </summary>
    public class ExternalUserBinding
    {
        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }


        /// <summary>
        /// 软件服务管理平台内部用户ID。
        ///
        /// 对应 Users 表：
        ///
        /// User.Id
        /// </summary>
        public int UserId { get; set; }


        /// <summary>
        /// 外部通知渠道。
        ///
        /// 当前支持：
        ///
        /// DingTalk
        ///
        /// 后续可以继续增加：
        ///
        /// WeCom
        /// Feishu
        /// </summary>
        public string Channel { get; set; }
            = string.Empty;


        /// <summary>
        /// 用户在外部平台中的唯一标识。
        ///
        /// 对于钉钉，
        /// 后续如果我们接入组织通讯录能力，
        /// 可以在这里保存钉钉 UserId。
        ///
        /// 当前第一版做群机器人 @ 人时，
        /// 可能主要使用 Mobile，
        /// 因此这个字段允许为空。
        /// </summary>
        public string? ExternalUserId { get; set; }


        /// <summary>
        /// 用户在外部平台绑定的手机号。
        ///
        /// 当前主要为钉钉机器人 @ 人预留。
        ///
        /// 注意：
        /// 手机号属于用户个人信息，
        /// 后续日志中不要直接输出完整手机号。
        /// </summary>
        public string? Mobile { get; set; }


        /// <summary>
        /// 当前绑定是否启用。
        ///
        /// false：
        /// 外部通知发送时应该忽略这条绑定。
        ///
        /// 这样员工更换账号时，
        /// 不一定需要直接删除历史绑定。
        /// </summary>
        public bool IsEnabled { get; set; }
            = true;


        /// <summary>
        /// 创建时间。
        /// 统一使用 UTC 时间保存。
        /// </summary>
        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;


        /// <summary>
        /// 最后更新时间。
        /// </summary>
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;


        /// <summary>
        /// 对应的软件服务平台用户。
        ///
        /// EF Core 导航属性。
        /// </summary>
        public User? User { get; set; }
    }
}