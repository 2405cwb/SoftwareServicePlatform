using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 系统默认通知策略初始化器。
    ///
    /// 主要职责：
    ///
    /// 第一次启动系统时，
    /// 自动向 NotificationPolicies
    /// 和 NotificationPolicyChannels
    /// 写入系统预定义的通知事件。
    ///
    /// 非常重要：
    ///
    /// Seeder 只负责“补缺失的数据”，
    /// 不覆盖已经存在的策略。
    ///
    /// 原因：
    ///
    /// 后续管理员可能在后台手工关闭：
    ///
    /// Ticket.Closed 的站内通知
    ///
    /// 或关闭：
    ///
    /// Ticket.CustomerReplied 的钉钉通知
    ///
    /// 如果每次启动都重新覆盖默认值，
    /// 管理员配置就没有意义了。
    /// </summary>
    public class NotificationPolicySeeder
    {
        private readonly AppDbContext _dbContext;

        private readonly ILogger<NotificationPolicySeeder>
            _logger;


        public NotificationPolicySeeder(
            AppDbContext dbContext,
            ILogger<NotificationPolicySeeder> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }


        /// <summary>
        /// 初始化系统缺失的默认通知策略。
        /// </summary>
        public async Task SeedAsync(
            CancellationToken cancellationToken = default)
        {
            /*
             * ==========================================
             * 系统默认策略
             * ==========================================
             *
             * 这里描述的是“第一次安装系统”的默认行为。
             *
             * 后续管理员可以通过数据库 / 管理页面
             * 修改这些配置。
             */
            var defaults =
                GetDefaultPolicies();


            foreach (var defaultPolicy in defaults)
            {
                /*
                 * 先检查这个 EventKey 是否已经存在。
                 *
                 * 如果存在：
                 *
                 * 不修改 IsEnabled
                 * 不修改 InAppEnabled
                 * 不修改 RecipientStrategy
                 * 不修改通知级别
                 *
                 * 防止覆盖管理员配置。
                 */
                var existingPolicy =
                    await _dbContext
                        .NotificationPolicies
                        .Include(x => x.Channels)
                        .FirstOrDefaultAsync(
                            x =>
                                x.EventKey ==
                                defaultPolicy.EventKey,
                            cancellationToken
                        );


                /*
                 * ==========================================
                 * 策略不存在
                 * ==========================================
                 *
                 * 第一次运行系统时会走这里。
                 */
                if (existingPolicy == null)
                {
                    _dbContext
                        .NotificationPolicies
                        .Add(defaultPolicy);

                    continue;
                }


                /*
                 * ==========================================
                 * 策略已存在
                 * ==========================================
                 *
                 * 策略本身不覆盖。
                 *
                 * 但是如果代码新增了某个渠道，
                 * 可以补充缺失的渠道配置。
                 */
                foreach (
                    var defaultChannel
                    in defaultPolicy.Channels)
                {
                    var channelExists =
                        existingPolicy.Channels.Any(
                            x =>
                                string.Equals(
                                    x.Channel,
                                    defaultChannel.Channel,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        );


                    if (channelExists)
                    {
                        /*
                         * 已存在则完全保留管理员配置。
                         */
                        continue;
                    }


                    existingPolicy.Channels.Add(
                        new NotificationPolicyChannel
                        {
                            Channel =
                                defaultChannel.Channel,

                            IsEnabled =
                                defaultChannel.IsEnabled,

                            MentionRecipient =
                                defaultChannel
                                    .MentionRecipient,

                            MentionAll =
                                defaultChannel.MentionAll
                        }
                    );
                }
            }


            await _dbContext.SaveChangesAsync(
                cancellationToken
            );


            _logger.LogInformation(
                "通知策略初始化完成"
            );
        }


        /// <summary>
        /// 定义系统第一版默认通知策略。
        ///
        /// 这里相当于整个系统通知规则的
        /// “默认配置清单”。
        /// </summary>
        private static List<NotificationPolicy>
            GetDefaultPolicies()
        {
            return new List<NotificationPolicy>
            {
                /*
                 * ==========================================
                 * 客户创建新工单
                 * ==========================================
                 *
                 * 新工单必须及时让售后知道。
                 *
                 * 站内：
                 * 通知所有 Support
                 *
                 * 钉钉：
                 * 发送一条群消息
                 *
                 * 当前不 @ 某一个售后，
                 * 因为这时候工单还没有具体处理人。
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketCreated,

                    eventName:
                        "客户创建工单",

                    description:
                        "客户从门户提交新工单后触发，默认通知所有售后人员。",

                    recipientStrategy:
                        RecipientStrategies.Support,

                    defaultLevel:
                        "Info",

                    dingTalkEnabled:
                        true,

                    mentionRecipient:
                        false
                ),


                /*
                 * ==========================================
                 * 售后完成分诊
                 * ==========================================
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketTriaged,

                    eventName:
                        "工单完成分诊",

                    description:
                        "售后确定工单优先级并完成分诊后触发，通知当前处理人。",

                    recipientStrategy:
                        RecipientStrategies.Assignee,

                    defaultLevel:
                        "Info",

                    dingTalkEnabled:
                        true,

                    mentionRecipient:
                        true
                ),


                /*
                 * ==========================================
                 * 工单第一次分配
                 * ==========================================
                 *
                 * 原本没有处理人，
                 * 现在第一次指定正式处理人。
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketAssigned,

                    eventName:
                        "工单分配",

                    description:
                        "未分配工单第一次指定处理人后触发，通知新的处理人。",

                    recipientStrategy:
                        RecipientStrategies.Assignee,

                    defaultLevel:
                        "Info",

                    dingTalkEnabled:
                        true,

                    mentionRecipient:
                        true
                ),

                /*
                 * ==========================================
                 * 工单重新分配
                 * ==========================================
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketReassigned,

                    eventName:
                        "工单重新分配",

                    description:
                        "工单处理人发生变化时触发，通知新的处理人。",

                    recipientStrategy:
                        RecipientStrategies.Assignee,

                    defaultLevel:
                        "Info",

                    dingTalkEnabled:
                        true,

                    mentionRecipient:
                        true
                ),


                /*
                 * ==========================================
                 * 客户追加回复
                 * ==========================================
                 *
                 * 有处理人：
                 * 通知处理人。
                 *
                 * 没有处理人：
                 * 通知售后。
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketCustomerReplied,

                    eventName:
                        "客户追加回复",

                    description:
                        "客户对工单追加回复或补充信息后触发。",

                    recipientStrategy:
                        RecipientStrategies
                            .AssigneeOrSupport,

                    defaultLevel:
                        "Info",

                    dingTalkEnabled:
                        true,

                    mentionRecipient:
                        true
                ),


                /*
                 * ==========================================
                 * 工作人员公开回复客户
                 * ==========================================
                 *
                 * Admin / Support / Developer
                 * 对客户增加可见回复后触发。
                 *
                 * 默认只发送站内通知，
                 * 不在内部钉钉群重复广播。
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketStaffReplied,

                    eventName:
                        "工作人员回复客户",

                    description:
                        "公司内部人员对工单进行公开回复后触发，通知该工单所属客户用户。",

                    recipientStrategy:
                        RecipientStrategies.Customer,

                    defaultLevel:
                        "Info",

                    dingTalkEnabled:
                        false,

                    mentionRecipient:
                        false
                ),

                /*
                 * ==========================================
                 * 工单重新打开
                 * ==========================================
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketReopened,

                    eventName:
                        "工单重新打开",

                    description:
                        "已经解决或关闭的工单被重新打开时触发。",

                    recipientStrategy:
                        RecipientStrategies
                            .AssigneeAndSupport,

                    defaultLevel:
                        "Warning",

                    dingTalkEnabled:
                        true,

                    mentionRecipient:
                        true
                ),


                /*
                 * ==========================================
                 * SLA 即将超时
                 * ==========================================
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketSlaWarning,

                    eventName:
                        "SLA 即将超时",

                    description:
                        "工单距离 SLA 截止时间较近时触发提醒。",

                    recipientStrategy:
                        RecipientStrategies
                            .AssigneeAndSupport,

                    defaultLevel:
                        "Warning",

                    dingTalkEnabled:
                        true,

                    mentionRecipient:
                        true
                ),


                /*
                 * ==========================================
                 * SLA 已经超时
                 * ==========================================
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketSlaOverdue,

                    eventName:
                        "SLA 已超时",

                    description:
                        "工单已经超过 SLA 时限后触发。",

                    recipientStrategy:
                        RecipientStrategies
                            .AssigneeSupportAdmin,

                    defaultLevel:
                        "Danger",

                    dingTalkEnabled:
                        true,

                    mentionRecipient:
                        true
                ),


                /*
                 * ==========================================
                 * 工单解决
                 * ==========================================
                 *
                 * 先只发送站内通知客户。
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketResolved,

                    eventName:
                        "工单已解决",

                    description:
                        "处理人员将工单标记为已解决后触发。",

                    recipientStrategy:
                        RecipientStrategies.Customer,

                    defaultLevel:
                        "Info",

                    dingTalkEnabled:
                        false,

                    mentionRecipient:
                        false
                ),


                /*
                 * ==========================================
                 * 工单关闭
                 * ==========================================
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .TicketClosed,

                    eventName:
                        "工单关闭",

                    description:
                        "工单正式关闭后触发。",

                    recipientStrategy:
                        RecipientStrategies
                            .CustomerAndAssignee,

                    defaultLevel:
                        "Info",

                    dingTalkEnabled:
                        false,

                    mentionRecipient:
                        false
                ),


                /*
                 * ==========================================
                 * 软件版本发布
                 * ==========================================
                 *
                 * 当前先开启站内通知。
                 *
                 * 钉钉默认关闭，
                 * 后面管理员可以按实际需求开启。
                 */
                CreatePolicy(
                    eventKey:
                        NotificationEventKeys
                            .VersionPublished,

                    eventName:
                        "软件版本发布",

                    description:
                        "软件版本正式发布后，通知本次发布范围内的客户用户。",

                    recipientStrategy:
                        RecipientStrategies
                            .VersionAudience,

                    defaultLevel:
                        "Info",

                    dingTalkEnabled:
                        false,

                    mentionRecipient:
                        false
                )
            };
        }


        /// <summary>
        /// 创建一条默认通知策略。
        ///
        /// 把大量重复的初始化代码统一封装，
        /// 方便以后继续增加新的通知事件。
        /// </summary>
        private static NotificationPolicy CreatePolicy(
            string eventKey,
            string eventName,
            string description,
            string recipientStrategy,
            string defaultLevel,
            bool dingTalkEnabled,
            bool mentionRecipient)
        {
            var now =
                DateTime.UtcNow;


            return new NotificationPolicy
            {
                EventKey =
                    eventKey,

                EventName =
                    eventName,

                Description =
                    description,

                IsEnabled =
                    true,

                InAppEnabled =
                    true,

                RecipientStrategy =
                    recipientStrategy,

                DefaultLevel =
                    defaultLevel,

                CreatedAt =
                    now,

                UpdatedAt =
                    now,

                /*
                 * 当前系统已经实现 DingTalk，
                 * 所以第一版先给每个事件
                 * 建立一个 DingTalk 渠道配置。
                 *
                 * 即使默认不发送，
                 * 也保存一条：
                 *
                 * IsEnabled = false
                 *
                 * 这样后续管理页面可以直接切换，
                 * 不需要临时新增记录。
                 */
                Channels =
                {
                    new NotificationPolicyChannel
                    {
                        Channel =
                            NotificationChannels
                                .DingTalk,

                        IsEnabled =
                            dingTalkEnabled,

                        MentionRecipient =
                            mentionRecipient,

                        MentionAll =
                            false,

                        CreatedAt =
                            now,

                        UpdatedAt =
                            now
                    }
                }
            };
        }
    }
}