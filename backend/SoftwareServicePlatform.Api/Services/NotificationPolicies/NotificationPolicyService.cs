using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 通知策略读取服务。
    ///
    /// 当前第一版直接读取 PostgreSQL。
    ///
    /// 暂时不做缓存。
    ///
    /// 原因：
    ///
    /// 1. 当前通知量很小
    /// 2. 数据库查询压力可以忽略
    /// 3. 管理员修改策略后可以立即生效
    ///
    /// 等以后通知量明显增加，
    /// 再增加 IMemoryCache 即可，
    /// 业务层不需要跟着修改。
    /// </summary>
    public class NotificationPolicyService
        : INotificationPolicyService
    {
        /// <summary>
        /// EF Core 数据库上下文。
        /// </summary>
        private readonly AppDbContext
            _dbContext;


        /// <summary>
        /// 日志服务。
        /// </summary>
        private readonly ILogger<
            NotificationPolicyService>
            _logger;


        public NotificationPolicyService(
            AppDbContext dbContext,
            ILogger<NotificationPolicyService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }


        /// <summary>
        /// 根据 EventKey 读取通知策略。
        /// </summary>
        public async Task<
            NotificationPolicySnapshot?>
            GetAsync(
                string eventKey,
                CancellationToken cancellationToken = default)
        {
            /*
             * ==========================================
             * 1. 参数检查
             * ==========================================
             */
            if (string.IsNullOrWhiteSpace(eventKey))
            {
                _logger.LogWarning(
                    "读取通知策略失败：EventKey 为空"
                );

                return null;
            }


            eventKey =
                eventKey.Trim();


            /*
             * ==========================================
             * 2. 查询数据库
             * ==========================================
             *
             * 使用 AsNoTracking：
             *
             * 因为这里只是读取配置，
             * 不需要 EF Core 跟踪这些实体。
             *
             * Include Channels：
             *
             * 一次把：
             *
             * DingTalk
             * WeCom
             * Feishu
             *
             * 等渠道配置一起读取出来。
             */
            var policy =
                await _dbContext
                    .NotificationPolicies
                    .AsNoTracking()
                    .Include(x => x.Channels)
                    .FirstOrDefaultAsync(
                        x =>
                            x.EventKey == eventKey,
                        cancellationToken
                    );


            /*
             * ==========================================
             * 3. 策略不存在
             * ==========================================
             *
             * 通知属于辅助能力。
             *
             * 不能因为管理员误删了一条通知策略，
             * 就导致：
             *
             * 工单创建失败
             * 工单分诊失败
             * 软件发布失败
             *
             * 所以这里只记录日志并返回 null。
             */
            if (policy == null)
            {
                _logger.LogWarning(
                    "没有找到通知策略。EventKey={EventKey}",
                    eventKey
                );

                return null;
            }


            /*
             * ==========================================
             * 4. Entity → Snapshot
             * ==========================================
             *
             * 不把 EF Entity 暴露给业务层。
             */
            return new NotificationPolicySnapshot
            {
                EventKey =
                    policy.EventKey,

                EventName =
                    policy.EventName,

                IsEnabled =
                    policy.IsEnabled,

                InAppEnabled =
                    policy.InAppEnabled,

                RecipientStrategy =
                    policy.RecipientStrategy,

                DefaultLevel =
                    policy.DefaultLevel,

                Channels =
                    policy.Channels
                        .OrderBy(x => x.Channel)
                        .Select(
                            x =>
                                new NotificationPolicyChannelSnapshot
                                {
                                    Channel =
                                        x.Channel,

                                    IsEnabled =
                                        x.IsEnabled,

                                    MentionRecipient =
                                        x.MentionRecipient,

                                    MentionAll =
                                        x.MentionAll
                                }
                        )
                        .ToList()
            };
        }
    }
}