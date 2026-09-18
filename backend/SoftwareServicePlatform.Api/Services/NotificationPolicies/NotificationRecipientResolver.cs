using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Services.NotificationPolicies
{
    /// <summary>
    /// 通知接收人统一解析器。
    ///
    /// 这个类集中处理：
    ///
    /// Support
    /// Assignee
    /// Customer
    /// AssigneeOrSupport
    /// AssigneeAndSupport
    /// AssigneeSupportAdmin
    /// CustomerAndAssignee
    /// VersionAudience
    ///
    /// 以后业务 Controller 不应该再自己写：
    ///
    /// _dbContext.Users
    ///     .Where(x => x.Role == "Support")
    ///
    /// 这种通知接收人查询。
    ///
    /// 所有规则统一收口到这里。
    /// </summary>
    public class NotificationRecipientResolver
        : INotificationRecipientResolver
    {
        private readonly AppDbContext
            _dbContext;

        private readonly ILogger<
            NotificationRecipientResolver>
            _logger;


        public NotificationRecipientResolver(
            AppDbContext dbContext,
            ILogger<NotificationRecipientResolver> logger)
        {
            _dbContext = dbContext;

            _logger = logger;
        }


        /// <summary>
        /// 根据策略解析最终接收人。
        /// </summary>
        public async Task<
            IReadOnlyList<NotificationRecipient>>
            ResolveAsync(
                string recipientStrategy,
                NotificationRecipientContext context,
                CancellationToken cancellationToken = default)
        {
            /*
             * ==========================================
             * 1. 基础参数检查
             * ==========================================
             */
            if (string.IsNullOrWhiteSpace(
                    recipientStrategy))
            {
                _logger.LogWarning(
                    "通知接收人解析失败：RecipientStrategy 为空"
                );

                return Array.Empty<
                    NotificationRecipient>();
            }


            /*
             * ==========================================
             * 2. 根据策略进入对应解析逻辑
             * ==========================================
             */
            switch (recipientStrategy)
            {
                /*
                 * 所有启用的售后人员。
                 */
                case RecipientStrategies.Support:

                    return await GetUsersByRoleAsync(
                        "Support",
                        cancellationToken
                    );


                /*
                 * 当前工单处理人。
                 */
                case RecipientStrategies.Assignee:

                    return await ResolveAssigneeAsync(
                        context,
                        cancellationToken
                    );


                /*
                 * 当前工单所属客户的
                 * 所有启用 Customer 用户。
                 */
                case RecipientStrategies.Customer:

                    return await ResolveCustomerAsync(
                        context,
                        cancellationToken
                    );


                /*
                 * 有处理人：
                 *      通知处理人
                 *
                 * 没处理人：
                 *      通知全部售后
                 */
                case RecipientStrategies
                    .AssigneeOrSupport:

                    return await ResolveAssigneeOrSupportAsync(
                        context,
                        cancellationToken
                    );


                /*
                 * 处理人 + 全部售后。
                 */
                case RecipientStrategies
                    .AssigneeAndSupport:

                    return await ResolveAssigneeAndSupportAsync(
                        context,
                        cancellationToken
                    );


                /*
                 * 处理人 + 售后 + 管理员。
                 *
                 * 主要用于 SLA 超时等严重事件。
                 */
                case RecipientStrategies
                    .AssigneeSupportAdmin:

                    return await ResolveAssigneeSupportAdminAsync(
                        context,
                        cancellationToken
                    );


                /*
                 * 客户用户 + 当前处理人。
                 */
                case RecipientStrategies
                    .CustomerAndAssignee:

                    return await ResolveCustomerAndAssigneeAsync(
                        context,
                        cancellationToken
                    );


                /*
                 * 软件版本真正发布到的客户用户。
                 */
                case RecipientStrategies
                    .VersionAudience:

                    return await ResolveVersionAudienceAsync(
                        context,
                        cancellationToken
                    );


                default:

                    /*
                     * 数据库中如果有人手工写错了策略，
                     * 不能因此让正常业务报错。
                     */
                    _logger.LogWarning(
                        "未知通知接收人策略。RecipientStrategy={RecipientStrategy}",
                        recipientStrategy
                    );

                    return Array.Empty<
                        NotificationRecipient>();
            }
        }


        // =====================================================
        // Ticket
        // =====================================================


        /// <summary>
        /// 获取当前工单处理人。
        /// </summary>
        private async Task<
            IReadOnlyList<NotificationRecipient>>
            ResolveAssigneeAsync(
                NotificationRecipientContext context,
                CancellationToken cancellationToken)
        {
            var ticket =
                await GetTicketContextAsync(
                    context,
                    cancellationToken
                );


            if (ticket == null
                ||
                !ticket.AssignedToUserId.HasValue)
            {
                return Array.Empty<
                    NotificationRecipient>();
            }


            var user =
                await GetUserAsync(
                    ticket.AssignedToUserId.Value,
                    cancellationToken
                );


            if (user == null)
            {
                return Array.Empty<
                    NotificationRecipient>();
            }


            return new[]
            {
                user
            };
        }


        /// <summary>
        /// 获取当前工单所属客户的用户。
        ///
        /// 当前第一版定义：
        ///
        /// Customer =
        /// 该客户下所有启用的 Customer 账号。
        ///
        /// 以后如果 Ticket 增加：
        ///
        /// ContactUserId
        ///
        /// 可以在这里统一改成
        /// “只通知具体联系人”，
        /// 上层业务完全不用调整。
        /// </summary>
        private async Task<
            IReadOnlyList<NotificationRecipient>>
            ResolveCustomerAsync(
                NotificationRecipientContext context,
                CancellationToken cancellationToken)
        {
            var ticket =
                await GetTicketContextAsync(
                    context,
                    cancellationToken
                );


            if (ticket == null)
            {
                return Array.Empty<
                    NotificationRecipient>();
            }


            return await GetCustomerUsersAsync(
                ticket.CustomerId,
                cancellationToken
            );
        }


        /// <summary>
        /// 优先解析工单处理人。
        ///
        /// 如果：
        ///
        /// AssignedToUserId == null
        ///
        /// 或处理人账号已经停用，
        ///
        /// 自动回退到所有 Support。
        ///
        /// 这个策略非常适合：
        ///
        /// 客户追加回复。
        /// </summary>
        private async Task<
            IReadOnlyList<NotificationRecipient>>
            ResolveAssigneeOrSupportAsync(
                NotificationRecipientContext context,
                CancellationToken cancellationToken)
        {
            var assignee =
                await ResolveAssigneeAsync(
                    context,
                    cancellationToken
                );


            /*
             * 有有效处理人，
             * 就只通知处理人。
             */
            if (assignee.Count > 0)
            {
                return assignee;
            }


            /*
             * 没有处理人，
             * 回退到全部售后。
             */
            return await GetUsersByRoleAsync(
                "Support",
                cancellationToken
            );
        }


        /// <summary>
        /// 当前处理人 + 所有售后。
        ///
        /// 最后统一去重。
        ///
        /// 因为处理人本身也可能就是 Support。
        /// </summary>
        private async Task<
            IReadOnlyList<NotificationRecipient>>
            ResolveAssigneeAndSupportAsync(
                NotificationRecipientContext context,
                CancellationToken cancellationToken)
        {
            var assignee =
                await ResolveAssigneeAsync(
                    context,
                    cancellationToken
                );


            var support =
                await GetUsersByRoleAsync(
                    "Support",
                    cancellationToken
                );


            return DistinctRecipients(
                assignee.Concat(support)
            );
        }


        /// <summary>
        /// 当前处理人 + Support + Admin。
        ///
        /// 用于比较严重的业务事件，
        /// 例如 SLA 已经超时。
        /// </summary>
        private async Task<
            IReadOnlyList<NotificationRecipient>>
            ResolveAssigneeSupportAdminAsync(
                NotificationRecipientContext context,
                CancellationToken cancellationToken)
        {
            var assignee =
                await ResolveAssigneeAsync(
                    context,
                    cancellationToken
                );


            var support =
                await GetUsersByRoleAsync(
                    "Support",
                    cancellationToken
                );


            var admins =
                await GetUsersByRoleAsync(
                    "Admin",
                    cancellationToken
                );


            return DistinctRecipients(
                assignee
                    .Concat(support)
                    .Concat(admins)
            );
        }


        /// <summary>
        /// 当前工单所属客户用户 + 当前处理人。
        ///
        /// 例如：
        ///
        /// 工单关闭后，
        /// 客户和处理人员都可以得到站内记录。
        /// </summary>
        private async Task<
            IReadOnlyList<NotificationRecipient>>
            ResolveCustomerAndAssigneeAsync(
                NotificationRecipientContext context,
                CancellationToken cancellationToken)
        {
            var customers =
                await ResolveCustomerAsync(
                    context,
                    cancellationToken
                );


            var assignee =
                await ResolveAssigneeAsync(
                    context,
                    cancellationToken
                );


            return DistinctRecipients(
                customers.Concat(assignee)
            );
        }


        // =====================================================
        // Version
        // =====================================================


        /// <summary>
        /// 解析软件版本真正的发布对象。
        ///
        /// 当前项目在版本发布时已经把：
        ///
        /// SoftwareVersionId
        /// +
        /// CustomerId
        ///
        /// 保存到：
        ///
        /// SoftwareVersionCustomers
        ///
        /// 所以这里不重新计算软件授权范围，
        /// 而是直接读取“发布时快照”。
        ///
        /// 这样以后即使客户的软件授权发生变化，
        /// 历史版本通知对象仍然保持正确。
        /// </summary>
        private async Task<
            IReadOnlyList<NotificationRecipient>>
            ResolveVersionAudienceAsync(
                NotificationRecipientContext context,
                CancellationToken cancellationToken)
        {
            if (!context.SoftwareVersionId.HasValue)
            {
                _logger.LogWarning(
                    "解析 VersionAudience 失败：没有提供 SoftwareVersionId"
                );

                return Array.Empty<
                    NotificationRecipient>();
            }


            /*
             * 查询这个版本真正发布过的客户。
             */
            var customerIds =
                await _dbContext
                    .SoftwareVersionCustomers
                    .AsNoTracking()
                    .Where(x =>
                        x.SoftwareVersionId ==
                        context.SoftwareVersionId.Value
                    )
                    .Select(x =>
                        x.CustomerId
                    )
                    .Distinct()
                    .ToListAsync(
                        cancellationToken
                    );


            if (customerIds.Count == 0)
            {
                return Array.Empty<
                    NotificationRecipient>();
            }


            /*
             * 再找到这些客户下面
             * 当前仍启用的 Customer 用户。
             */
            return await _dbContext.Users
                .AsNoTracking()
                .Where(x =>
                    x.IsEnabled
                    &&
                    x.Role == "Customer"
                    &&
                    x.CustomerId.HasValue
                    &&
                    customerIds.Contains(
                        x.CustomerId.Value
                    )
                )
                .OrderBy(x =>
                    x.DisplayName
                )
                .Select(x =>
                    new NotificationRecipient
                    {
                        UserId =
                            x.Id,

                        DisplayName =
                            x.DisplayName,

                        Role =
                            x.Role,

                        CustomerId =
                            x.CustomerId
                    }
                )
                .ToListAsync(
                    cancellationToken
                );
        }


        // =====================================================
        // Common queries
        // =====================================================


        /// <summary>
        /// 根据角色查询所有启用用户。
        /// </summary>
        private async Task<
            IReadOnlyList<NotificationRecipient>>
            GetUsersByRoleAsync(
                string role,
                CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(x =>
                    x.IsEnabled
                    &&
                    x.Role == role
                )
                .OrderBy(x =>
                    x.DisplayName
                )
                .Select(x =>
                    new NotificationRecipient
                    {
                        UserId =
                            x.Id,

                        DisplayName =
                            x.DisplayName,

                        Role =
                            x.Role,

                        CustomerId =
                            x.CustomerId
                    }
                )
                .ToListAsync(
                    cancellationToken
                );
        }


        /// <summary>
        /// 根据用户 ID 查询一个启用用户。
        ///
        /// 如果用户已经被停用，
        /// 则认为当前不能再接收通知。
        /// </summary>
        private async Task<NotificationRecipient?>
            GetUserAsync(
                int userId,
                CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(x =>
                    x.Id == userId
                    &&
                    x.IsEnabled
                )
                .Select(x =>
                    new NotificationRecipient
                    {
                        UserId =
                            x.Id,

                        DisplayName =
                            x.DisplayName,

                        Role =
                            x.Role,

                        CustomerId =
                            x.CustomerId
                    }
                )
                .FirstOrDefaultAsync(
                    cancellationToken
                );
        }


        /// <summary>
        /// 查询某一个客户下面
        /// 所有启用的 Customer 用户。
        /// </summary>
        private async Task<
            IReadOnlyList<NotificationRecipient>>
            GetCustomerUsersAsync(
                int customerId,
                CancellationToken cancellationToken)
        {
            return await _dbContext.Users
                .AsNoTracking()
                .Where(x =>
                    x.IsEnabled
                    &&
                    x.Role == "Customer"
                    &&
                    x.CustomerId == customerId
                )
                .OrderBy(x =>
                    x.DisplayName
                )
                .Select(x =>
                    new NotificationRecipient
                    {
                        UserId =
                            x.Id,

                        DisplayName =
                            x.DisplayName,

                        Role =
                            x.Role,

                        CustomerId =
                            x.CustomerId
                    }
                )
                .ToListAsync(
                    cancellationToken
                );
        }


        /// <summary>
        /// 读取通知解析真正需要的
        /// 最小工单上下文。
        ///
        /// 不加载完整 Ticket Entity，
        /// 只查询：
        ///
        /// CustomerId
        /// AssignedToUserId
        ///
        /// 减少无意义的数据加载。
        /// </summary>
        private async Task<TicketRecipientContext?>
            GetTicketContextAsync(
                NotificationRecipientContext context,
                CancellationToken cancellationToken)
        {
            if (!context.TicketId.HasValue)
            {
                _logger.LogWarning(
                    "解析工单通知接收人失败：没有提供 TicketId"
                );

                return null;
            }


            var ticket =
                await _dbContext.Tickets
                    .AsNoTracking()
                    .Where(x =>
                        x.Id ==
                        context.TicketId.Value
                    )
                    .Select(x =>
                        new TicketRecipientContext
                        {
                            CustomerId =
                                x.CustomerId,

                            AssignedToUserId =
                                x.AssignedToUserId
                        }
                    )
                    .FirstOrDefaultAsync(
                        cancellationToken
                    );


            if (ticket == null)
            {
                _logger.LogWarning(
                    "解析通知接收人失败：工单不存在。TicketId={TicketId}",
                    context.TicketId.Value
                );
            }


            return ticket;
        }


        /// <summary>
        /// 合并多个接收人集合并按照 UserId 去重。
        ///
        /// 例如：
        ///
        /// 当前处理人本身就是 Support，
        ///
        /// AssigneeAndSupport
        ///
        /// 不应该让同一个人收到两条通知。
        /// </summary>
        private static IReadOnlyList<
            NotificationRecipient>
            DistinctRecipients(
                IEnumerable<NotificationRecipient>
                    recipients)
        {
            return recipients
                .GroupBy(x =>
                    x.UserId
                )
                .Select(x =>
                    x.First()
                )
                .OrderBy(x =>
                    x.DisplayName
                )
                .ToList();
        }


        /// <summary>
        /// Resolver 内部使用的最小工单数据。
        ///
        /// 不暴露给其他业务模块。
        /// </summary>
        private class TicketRecipientContext
        {
            public int CustomerId
            {
                get;
                init;
            }


            public int? AssignedToUserId
            {
                get;
                init;
            }
        }
    }
}