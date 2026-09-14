using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 平台数据统计。
    ///
    /// 第一阶段先提供首页顶部 KPI 数据。
    ///
    /// 后面再逐步增加：
    ///
    /// 工单趋势
    /// 工单状态分布
    /// 软件问题排行
    /// 客户问题排行
    /// 下载排行
    /// SLA 等。
    /// </summary>
    [ApiController]
    [Route("api/dashboard")]
    [Authorize(Roles = "Admin,Support")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _dbContext;


        public DashboardController(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        /// <summary>
        /// 获取 Dashboard 顶部汇总数据。
        ///
        /// GET:
        ///
        /// /api/dashboard/summary
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            /*
             * 当前所有业务时间都使用 UTC。
             */
            var now =
                DateTime.UtcNow;


            /*
             * 本月第一天。
             *
             * 例如：
             *
             * 当前时间：
             * 2026-09-14
             *
             * monthStart：
             * 2026-09-01 00:00:00 UTC
             */
            var monthStart =
                new DateTime(
                    now.Year,
                    now.Month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc
                );


            /*
             * ==========================================
             * 1. 当前启用客户数量
             * ==========================================
             */
            var activeCustomers =
                await _dbContext.Customers

                    .AsNoTracking()

                    .CountAsync(
                        x => x.IsEnabled
                    );


            /*
             * ==========================================
             * 2. 当前启用软件数量
             * ==========================================
             */
            var activeSoftwares =
                await _dbContext.Softwares

                    .AsNoTracking()

                    .CountAsync(
                        x => x.IsEnabled
                    );


            /*
             * ==========================================
             * 3. 工单总数
             * ==========================================
             */
            var totalTickets =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .CountAsync();


            /*
             * ==========================================
             * 4. 当前未结束工单数量
             * ==========================================
             *
             * Pending
             * +
             * Processing
             *
             * 都认为属于“待处理中的工单”。
             */
            var openTickets =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .CountAsync(
                        x =>
                            x.Status == "Pending"
                            ||
                            x.Status == "Processing"
                    );


            /*
             * ==========================================
             * 5. 本月新增工单
             * ==========================================
             */
            var thisMonthTickets =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .CountAsync(
                        x =>
                            x.CreatedAt >= monthStart
                    );


            /*
             * ==========================================
             * 6. 平均首次响应时间
             * ==========================================
             *
             * 只统计已经产生 FirstResponseAt 的工单。
             *
             * 旧工单因为历史上没有记录该字段，
             * FirstResponseAt 可能为 null，
             * 所以不能参与平均值计算。
             */
            var responseTimes =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.FirstResponseAt.HasValue
                    )

                    .Select(
                        x => new
                        {
                            x.CreatedAt,

                            FirstResponseAt =
                                x.FirstResponseAt!.Value
                        }
                    )

                    .ToListAsync();


            /*
             * 如果目前还没有任何响应数据，
             * 返回 null。
             *
             * 前端以后可以显示：
             *
             * -
             *
             * 而不是误导用户显示：
             *
             * 0分钟
             */
            double? averageFirstResponseMinutes =
                null;


            if (responseTimes.Count > 0)
            {
                averageFirstResponseMinutes =
                    responseTimes.Average(
                        x =>
                            (
                                x.FirstResponseAt
                                -
                                x.CreatedAt
                            )
                            .TotalMinutes
                    );


                /*
                 * 保留1位小数。
                 *
                 * 例如：
                 *
                 * 18.36666
                 *
                 * ↓
                 *
                 * 18.4 分钟
                 */
                averageFirstResponseMinutes =
                    Math.Round(
                        averageFirstResponseMinutes.Value,
                        1
                    );
            }


            /*
             * ==========================================
             * 返回 Dashboard 汇总数据
             * ==========================================
             */
            return Ok(
                new
                {
                    /*
                     * 启用客户
                     */
                    activeCustomers,


                    /*
                     * 启用软件
                     */
                    activeSoftwares,


                    /*
                     * 历史工单总数
                     */
                    totalTickets,


                    /*
                     * Pending + Processing
                     */
                    openTickets,


                    /*
                     * 本月新增
                     */
                    thisMonthTickets,


                    /*
                     * 平均首次响应分钟数。
                     *
                     * 没有数据时为 null。
                     */
                    averageFirstResponseMinutes,


                    /*
                     * 统计生成时间。
                     *
                     * 以后如果做自动刷新，
                     * 前端可以使用。
                     */
                    generatedAtUtc =
                        now
                }
            );
        }
        /// <summary>
        /// 获取工单状态分布。
        ///
        /// GET:
        ///
        /// /api/dashboard/ticket-status
        ///
        /// 返回：
        /// Pending
        /// Processing
        /// Resolved
        /// Closed
        ///
        /// 即使某一种状态当前数量为0，
        /// 也仍然返回，方便前端直接画图。
        /// </summary>
        [HttpGet("ticket-status")]
        public async Task<IActionResult> GetTicketStatus()
        {
            /*
             * 先从数据库按照 Status 分组统计。
             *
             * 得到类似：
             *
             * Pending      3
             * Processing   5
             * Closed       20
             *
             * 如果当前没有 Resolved，
             * 数据库结果里面可能根本没有这一项。
             */
            var statistics =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .GroupBy(
                        x => x.Status
                    )

                    .Select(
                        group => new
                        {
                            Status =
                                group.Key,

                            Count =
                                group.Count()
                        }
                    )

                    .ToListAsync();


            /*
             * 转成 Dictionary，
             * 后面读取更方便。
             */
            var statusMap =
                statistics.ToDictionary(
                    x => x.Status,
                    x => x.Count
                );


            /*
             * 我们固定返回这4种状态。
             *
             * 即使数据库中某种状态不存在，
             * Count 也返回0。
             *
             * 这样前端不需要自己补数据。
             */
            var result =
                new[]
                {
            new
            {
                status = "Pending",

                name = "待处理",

                count =
                    statusMap.GetValueOrDefault(
                        "Pending",
                        0
                    )
            },

            new
            {
                status = "Processing",

                name = "处理中",

                count =
                    statusMap.GetValueOrDefault(
                        "Processing",
                        0
                    )
            },

            new
            {
                status = "Resolved",

                name = "已解决",

                count =
                    statusMap.GetValueOrDefault(
                        "Resolved",
                        0
                    )
            },

            new
            {
                status = "Closed",

                name = "已关闭",

                count =
                    statusMap.GetValueOrDefault(
                        "Closed",
                        0
                    )
            }
                };


            return Ok(result);
        }
        /// <summary>
        /// 获取最近30天新建工单趋势。
        ///
        /// GET:
        ///
        /// /api/dashboard/ticket-trend
        ///
        /// 返回每天创建了多少工单。
        /// </summary>
        [HttpGet("ticket-trend")]
        public async Task<IActionResult> GetTicketTrend()
        {
            /*
             * 今天UTC日期。
             *
             * 例如：
             *
             * 2026-09-14 00:00:00
             */
            var today =
                DateTime.UtcNow.Date;


            /*
             * 包含今天一共30天。
             *
             * 今天算第1天，
             * 所以向前29天。
             */
            var startDate =
                today.AddDays(-29);


            /*
             * 查询最近30天创建的工单。
             *
             * 这里暂时只取 CreatedAt，
             * 不需要把整个 Ticket 查询出来。
             */
            var ticketDates =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.CreatedAt >= startDate
                    )

                    .Select(
                        x => x.CreatedAt
                    )

                    .ToListAsync();


            /*
             * 在内存中按照日期分组。
             *
             * 为什么这里不用复杂的数据库日期函数？
             *
             * 因为目前数据量还不大，
             * 而且这样代码更容易理解，
             * PostgreSQL / EF Core 也不容易遇到
             * 日期函数翻译问题。
             */
            var countByDate =
                ticketDates

                    .GroupBy(
                        x => x.Date
                    )

                    .ToDictionary(
                        group => group.Key,
                        group => group.Count()
                    );


            /*
             * 生成完整30天。
             *
             * 非常重要：
             *
             * 如果某一天没有任何工单，
             * 也必须返回：
             *
             * count = 0
             *
             * 否则前端折线图会缺日期。
             */
            var result =
                Enumerable
                    .Range(
                        0,
                        30
                    )

                    .Select(
                        index =>
                        {
                            var date =
                                startDate.AddDays(
                                    index
                                );


                            return new
                            {
                                /*
                                 * 返回 yyyy-MM-dd，
                                 * 前端处理最简单。
                                 */
                                date =
                                    date.ToString(
                                        "yyyy-MM-dd"
                                    ),


                                count =
                                    countByDate
                                        .GetValueOrDefault(
                                            date,
                                            0
                                        )
                            };
                        }
                    )

                    .ToList();


            return Ok(result);
        }

        /// <summary>
        /// 工单数量最多的软件 TOP5。
        ///
        /// GET:
        ///
        /// /api/dashboard/software-ticket-ranking
        /// </summary>
        [HttpGet("software-ticket-ranking")]
        public async Task<IActionResult> GetSoftwareTicketRanking()
        {
            var result =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .GroupBy(
                        x => new
                        {
                            x.SoftwareId,
                            SoftwareName = x.Software.Name
                        }
                    )

                    .Select(
                        group => new
                        {
                            softwareId =
                                group.Key.SoftwareId,

                            softwareName =
                                group.Key.SoftwareName,

                            ticketCount =
                                group.Count()
                        }
                    )

                    .OrderByDescending(
                        x => x.ticketCount
                    )

                    .ThenBy(
                        x => x.softwareName
                    )

                    .Take(5)

                    .ToListAsync();


            return Ok(result);
        }

        /// <summary>
        /// 提交工单数量最多的客户 TOP5。
        ///
        /// GET:
        ///
        /// /api/dashboard/customer-ticket-ranking
        /// </summary>
        [HttpGet("customer-ticket-ranking")]
        public async Task<IActionResult> GetCustomerTicketRanking()
        {
            var result =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .GroupBy(
                        x => new
                        {
                            x.CustomerId,
                            CustomerName = x.Customer.Name
                        }
                    )

                    .Select(
                        group => new
                        {
                            customerId =
                                group.Key.CustomerId,

                            customerName =
                                group.Key.CustomerName,

                            ticketCount =
                                group.Count()
                        }
                    )

                    .OrderByDescending(
                        x => x.ticketCount
                    )

                    .ThenBy(
                        x => x.customerName
                    )

                    .Take(5)

                    .ToListAsync();


            return Ok(result);
        }

        /// <summary>
        /// Dashboard 最近工单。
        ///
        /// GET:
        ///
        /// /api/dashboard/recent-tickets
        ///
        /// 当前返回最近8条。
        /// </summary>
        [HttpGet("recent-tickets")]
        public async Task<IActionResult> GetRecentTickets()
        {
            var result =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .OrderByDescending(
                        x => x.CreatedAt
                    )

                    .Take(8)

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.TicketNo,

                            x.Title,

                            x.Status,

                            x.Priority,

                            x.Source,

                            customerName =
                                x.Customer.Name,

                            softwareName =
                                x.Software.Name,

                            assignedToName =
                                x.AssignedToUser == null
                                    ? null
                                    : x.AssignedToUser.DisplayName,

                            x.CreatedAt,

                            x.UpdatedAt
                        }
                    )

                    .ToListAsync();


            return Ok(result);
        }
        /// <summary>
        /// 下载数据汇总。
        ///
        /// GET:
        /// /api/dashboard/download-summary
        /// </summary>
        [HttpGet("download-summary")]
        public async Task<IActionResult> GetDownloadSummary()
        {
            var now =
                DateTime.UtcNow;

            var monthStart =
                new DateTime(
                    now.Year,
                    now.Month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc
                );


            /*
             * 历史累计下载次数。
             */
            var totalDownloads =
                await _dbContext.DownloadRecords
                    .AsNoTracking()
                    .CountAsync();


            /*
             * 本月下载次数。
             */
            var thisMonthDownloads =
                await _dbContext.DownloadRecords
                    .AsNoTracking()
                    .CountAsync(
                        x =>
                            x.DownloadedAt >= monthStart
                    );


            return Ok(
                new
                {
                    totalDownloads,

                    thisMonthDownloads
                }
            );
        }
        
      
        /// <summary>
        /// 最近30天软件下载趋势。
        ///
        /// GET:
        /// /api/dashboard/download-trend
        /// </summary>
        [HttpGet("download-trend")]
        public async Task<IActionResult> GetDownloadTrend()
        {
            var today =
                DateTime.UtcNow.Date;

            /*
             * 包含今天，共30天。
             */
            var startDate =
                today.AddDays(-29);


            /*
             * 这里只取统计真正需要的时间字段。
             */
            var records =
                await _dbContext.DownloadRecords

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.DownloadedAt >= startDate
                    )

                    .Select(
                        x =>
                            x.DownloadedAt
                    )

                    .ToListAsync();


            /*
             * 按日期统计下载次数。
             */
            var countMap =
                records

                    .GroupBy(
                        x =>
                            x.Date
                    )

                    .ToDictionary(
                        x => x.Key,

                        x => x.Count()
                    );


            /*
             * 即使某一天没有下载，
             * 也要返回 count = 0。
             *
             * 否则前端折线图日期会断掉。
             */
            var result =
                Enumerable.Range(
                    0,
                    30
                )

                .Select(
                    index =>
                    {
                        var date =
                            startDate.AddDays(
                                index
                            );

                        return new
                        {
                            date =
                                date.ToString(
                                    "yyyy-MM-dd"
                                ),

                            count =
                                countMap
                                    .GetValueOrDefault(
                                        date,
                                        0
                                    )
                        };
                    }
                )

                .ToList();


            return Ok(result);
        }
        /// <summary>
        /// 下载次数最多的软件 TOP5。
        ///
        /// GET:
        /// /api/dashboard/software-download-ranking
        /// </summary>
        [HttpGet("software-download-ranking")]
        public async Task<IActionResult> GetSoftwareDownloadRanking()
        {
            var result =
                await _dbContext.DownloadRecords

                    .AsNoTracking()

                    /*
                     * 使用下载时的软件名称快照。
                     */
                    .GroupBy(
                        x => new
                        {
                            x.SoftwareId,

                            x.SoftwareName
                        }
                    )

                    .Select(
                        group => new
                        {
                            softwareId =
                                group.Key.SoftwareId,

                            softwareName =
                                group.Key.SoftwareName,

                            downloadCount =
                                group.Count()
                        }
                    )

                    .OrderByDescending(
                        x =>
                            x.downloadCount
                    )

                    .ThenBy(
                        x =>
                            x.softwareName
                    )

                    .Take(5)

                    .ToListAsync();


            return Ok(result);
        }

        /// <summary>
        /// 软件下载安装次数最多的客户 TOP5。
        ///
        /// GET:
        /// /api/dashboard/customer-download-ranking
        /// </summary>
        [HttpGet("customer-download-ranking")]
        public async Task<IActionResult> GetCustomerDownloadRanking()
        {
            var result =
                await _dbContext.DownloadRecords

                    .AsNoTracking()

                    .GroupBy(
                        x => new
                        {
                            x.CustomerId,

                            x.CustomerName
                        }
                    )

                    .Select(
                        group => new
                        {
                            customerId =
                                group.Key.CustomerId,

                            customerName =
                                group.Key.CustomerName,

                            downloadCount =
                                group.Count()
                        }
                    )

                    .OrderByDescending(
                        x =>
                            x.downloadCount
                    )

                    .ThenBy(
                        x =>
                            x.customerName
                    )

                    .Take(5)

                    .ToListAsync();


            return Ok(result);
        }

        /// <summary>
        /// 工单处理效率汇总。
        ///
        /// GET:
        ///
        /// /api/dashboard/ticket-efficiency-summary
        ///
        /// 这一版不涉及具体 SLA 阈值，
        /// 先统计平台当前真实的响应和解决效率。
        /// </summary>
        [HttpGet("ticket-efficiency-summary")]
        public async Task<IActionResult> GetTicketEfficiencySummary()
        {
            /*
             * ==========================================
             * 1. 工单总数
             * ==========================================
             */
            var totalTickets =
                await _dbContext.Tickets
                    .AsNoTracking()
                    .CountAsync();


            /*
             * ==========================================
             * 2. 当前未结束工单
             * ==========================================
             *
             * Pending
             * Processing
             *
             * 都认为仍处于处理中。
             */
            var openTickets =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .CountAsync(
                        x =>
                            x.Status == "Pending"
                            ||
                            x.Status == "Processing"
                    );


            /*
             * ==========================================
             * 3. 首次响应时间样本
             * ==========================================
             *
             * 旧工单可能没有 FirstResponseAt，
             * 所以只统计有真实时间的数据。
             */
            var responseSamples =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.FirstResponseAt.HasValue
                    )

                    .Select(
                        x => new
                        {
                            x.CreatedAt,

                            FirstResponseAt =
                                x.FirstResponseAt!.Value
                        }
                    )

                    .ToListAsync();


            /*
             * 过滤异常历史数据：
             *
             * FirstResponseAt
             * 不应该早于 CreatedAt。
             */
            var validResponseSamples =
                responseSamples

                    .Where(
                        x =>
                            x.FirstResponseAt
                            >=
                            x.CreatedAt
                    )

                    .ToList();


            double? averageFirstResponseMinutes =
                null;


            if (validResponseSamples.Count > 0)
            {
                averageFirstResponseMinutes =
                    validResponseSamples.Average(
                        x =>
                            (
                                x.FirstResponseAt
                                -
                                x.CreatedAt
                            )
                            .TotalMinutes
                    );


                averageFirstResponseMinutes =
                    Math.Round(
                        averageFirstResponseMinutes.Value,
                        1
                    );
            }


            /*
             * ==========================================
             * 4. 解决时间样本
             * ==========================================
             *
             * 当前 ResolvedAt 的语义：
             *
             * 工单当前最近一次解决时间。
             *
             * Reopen 后会被清空，
             * 因此重新打开的工单不会误算成
             * 已经解决。
             */
            var resolutionSamples =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.ResolvedAt.HasValue
                    )

                    .Select(
                        x => new
                        {
                            x.CreatedAt,

                            ResolvedAt =
                                x.ResolvedAt!.Value
                        }
                    )

                    .ToListAsync();


            var validResolutionSamples =
                resolutionSamples

                    .Where(
                        x =>
                            x.ResolvedAt
                            >=
                            x.CreatedAt
                    )

                    .ToList();


            double? averageResolutionMinutes =
                null;


            if (validResolutionSamples.Count > 0)
            {
                averageResolutionMinutes =
                    validResolutionSamples.Average(
                        x =>
                            (
                                x.ResolvedAt
                                -
                                x.CreatedAt
                            )
                            .TotalMinutes
                    );


                averageResolutionMinutes =
                    Math.Round(
                        averageResolutionMinutes.Value,
                        1
                    );
            }


            /*
             * ==========================================
             * 5. 关闭时间
             * ==========================================
             */
            var closeSamples =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.ClosedAt.HasValue
                    )

                    .Select(
                        x => new
                        {
                            x.CreatedAt,

                            ClosedAt =
                                x.ClosedAt!.Value
                        }
                    )

                    .ToListAsync();


            var validCloseSamples =
                closeSamples

                    .Where(
                        x =>
                            x.ClosedAt
                            >=
                            x.CreatedAt
                    )

                    .ToList();


            double? averageCloseMinutes =
                null;


            if (validCloseSamples.Count > 0)
            {
                averageCloseMinutes =
                    validCloseSamples.Average(
                        x =>
                            (
                                x.ClosedAt
                                -
                                x.CreatedAt
                            )
                            .TotalMinutes
                    );


                averageCloseMinutes =
                    Math.Round(
                        averageCloseMinutes.Value,
                        1
                    );
            }


            /*
             * ==========================================
             * 6. 响应率
             * ==========================================
             */
            var respondedTickets =
                validResponseSamples.Count;


            var responseRate =
                totalTickets == 0
                    ? 0
                    : Math.Round(
                        respondedTickets
                        * 100.0
                        /
                        totalTickets,
                        1
                    );


            /*
             * ==========================================
             * 7. 当前已解决数量 / 解决率
             * ==========================================
             *
             * Resolved + Closed
             *
             * 都认为当前已经解决。
             */
            var resolvedTickets =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .CountAsync(
                        x =>
                            x.Status == "Resolved"
                            ||
                            x.Status == "Closed"
                    );


            var resolutionRate =
                totalTickets == 0
                    ? 0
                    : Math.Round(
                        resolvedTickets
                        * 100.0
                        /
                        totalTickets,
                        1
                    );


            /*
             * ==========================================
             * 8. 返回结果
             * ==========================================
             */
            return Ok(
                new
                {
                    totalTickets,

                    openTickets,

                    respondedTickets,

                    resolvedTickets,

                    responseRate,

                    resolutionRate,

                    averageFirstResponseMinutes,

                    averageResolutionMinutes,

                    averageCloseMinutes,

                    /*
                     * 同时返回有效样本数，
                     * 以后看到平均值时能知道
                     * 它是基于多少条工单计算出来的。
                     */
                    responseSampleCount =
                        validResponseSamples.Count,

                    resolutionSampleCount =
                        validResolutionSamples.Count,

                    closeSampleCount =
                        validCloseSamples.Count
                }
            );
        }

        /// <summary>
        /// Support / Developer 工单处理效率排行。
        ///
        /// GET:
        /// /api/dashboard/staff-ticket-efficiency
        ///
        /// 统计内容：
        ///
        /// 1. 当前处理中工单数
        /// 2. 当前已解决工单数
        /// 3. 平均解决时长
        ///
        /// 注意：
        ///
        /// “当前处理人”使用 AssignedToUserId。
        ///
        /// “解决人”不能直接使用 AssignedToUserId，
        /// 而是查找当前已解决工单最后一条 Resolve 处理记录。
        /// </summary>
        [HttpGet("staff-ticket-efficiency")]
        public async Task<IActionResult> GetStaffTicketEfficiency()
        {
            /*
             * ==========================================
             * 1. 查询当前启用的 Support / Developer
             * ==========================================
             */
            var staffUsers =
                await _dbContext.Users

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.IsEnabled
                            &&
                            (
                                x.Role == "Support"
                                ||
                                x.Role == "Developer"
                            )
                    )

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.Username,

                            x.DisplayName,

                            x.Role
                        }
                    )

                    .ToListAsync();


            /*
             * ==========================================
             * 2. 当前正在处理的工单
             * ==========================================
             *
             * Pending / Processing
             *
             * 并且已经分配给某个员工。
             */
            var openTicketCounts =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.AssignedToUserId.HasValue
                            &&
                            (
                                x.Status == "Pending"
                                ||
                                x.Status == "Processing"
                            )
                    )

                    .GroupBy(
                        x =>
                            x.AssignedToUserId!.Value
                    )

                    .Select(
                        group => new
                        {
                            UserId =
                                group.Key,

                            Count =
                                group.Count()
                        }
                    )

                    .ToDictionaryAsync(
                        x => x.UserId,

                        x => x.Count
                    );


            /*
             * ==========================================
             * 3. 查询当前已经解决的工单
             * ==========================================
             *
             * Resolved
             * Closed
             *
             * 都认为当前已经解决。
             *
             * Reopen 后 Status 会变成 Processing，
             * 因此不会误算。
             */
            var resolvedTickets =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .Where(
                        x =>
                            (
                                x.Status == "Resolved"
                                ||
                                x.Status == "Closed"
                            )
                            &&
                            x.ResolvedAt.HasValue
                    )

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.CreatedAt,

                            ResolvedAt =
                                x.ResolvedAt!.Value
                        }
                    )

                    .ToListAsync();


            /*
             * ==========================================
             * 4. 找这些工单对应的 Resolve 操作记录
             * ==========================================
             */
            var resolvedTicketIds =
                resolvedTickets

                    .Select(
                        x => x.Id
                    )

                    .ToList();


            var resolveRecords =
                await _dbContext.TicketRecords

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.RecordType == "Resolve"
                            &&
                            resolvedTicketIds.Contains(
                                x.TicketId
                            )
                    )

                    .Select(
                        x => new
                        {
                            x.TicketId,

                            x.CreatedByUserId,

                            x.CreatedAt
                        }
                    )

                    .ToListAsync();


            /*
             * ==========================================
             * 5. 每张工单只认最后一次 Resolve
             * ==========================================
             *
             * 为什么不是第一条？
             *
             * 因为：
             *
             * Resolve
             * ↓
             * Reopen
             * ↓
             * Resolve
             *
             * 当前真正有效的是最后一次解决。
             */
            var latestResolveRecordMap =
                resolveRecords

                    .GroupBy(
                        x => x.TicketId
                    )

                    .ToDictionary(
                        group =>
                            group.Key,

                        group =>
                            group

                                .OrderByDescending(
                                    x => x.CreatedAt
                                )

                                .First()
                    );


            /*
             * ==========================================
             * 6. 为每个员工计算指标
             * ==========================================
             */
            var result =
                staffUsers

                    .Select(
                        staff =>
                        {
                            /*
                             * 当前正在处理多少工单。
                             */
                            var openTicketCount =
                                openTicketCounts
                                    .GetValueOrDefault(
                                        staff.Id,
                                        0
                                    );


                            /*
                             * 找当前最终由这个人解决的工单。
                             */
                            var staffResolvedTickets =
                                resolvedTickets

                                    .Where(
                                        ticket =>
                                        {
                                            if (
                                                !latestResolveRecordMap
                                                    .TryGetValue(
                                                        ticket.Id,
                                                        out var resolveRecord
                                                    )
                                            )
                                            {
                                                return false;
                                            }


                                            return
                                                resolveRecord.CreatedByUserId
                                                ==
                                                staff.Id;
                                        }
                                    )

                                    .ToList();


                            var resolvedTicketCount =
                                staffResolvedTickets.Count;


                            /*
                             * ==================================
                             * 平均解决时长
                             * ==================================
                             *
                             * CreatedAt
                             * →
                             * 当前有效的 ResolvedAt
                             */
                            var validSamples =
                                staffResolvedTickets

                                    .Where(
                                        x =>
                                            x.ResolvedAt
                                            >=
                                            x.CreatedAt
                                    )

                                    .ToList();


                            double? averageResolutionMinutes =
                                null;


                            if (validSamples.Count > 0)
                            {
                                averageResolutionMinutes =
                                    validSamples.Average(
                                        x =>
                                            (
                                                x.ResolvedAt
                                                -
                                                x.CreatedAt
                                            )
                                            .TotalMinutes
                                    );


                                averageResolutionMinutes =
                                    Math.Round(
                                        averageResolutionMinutes.Value,
                                        1
                                    );
                            }


                            return new
                            {
                                userId =
                                    staff.Id,

                                username =
                                    staff.Username,

                                displayName =
                                    staff.DisplayName,

                                role =
                                    staff.Role,

                                /*
                                 * 当前工作量。
                                 */
                                openTicketCount,

                                /*
                                 * 当前有效的已解决工单数量。
                                 */
                                resolvedTicketCount,

                                /*
                                 * 平均解决时长。
                                 */
                                averageResolutionMinutes,

                                /*
                                 * 平均值实际使用多少样本。
                                 */
                                resolutionSampleCount =
                                    validSamples.Count
                            };
                        }
                    )

                    /*
                     * 第一排序：
                     * 已解决数量多的在前。
                     */
                    .OrderByDescending(
                        x =>
                            x.resolvedTicketCount
                    )

                    /*
                     * 第二排序：
                     * 同样解决数量时，
                     * 当前积压少的在前。
                     */
                    .ThenBy(
                        x =>
                            x.openTicketCount
                    )

                    .ToList();


            return Ok(result);
        }

        /// <summary>
        /// SLA 汇总统计。
        ///
        /// GET:
        /// /api/dashboard/ticket-sla-summary
        ///
        /// 只统计创建工单时已经保存 SLA 快照的工单。
        ///
        /// 旧工单 SLA 字段为 null，
        /// 不参与 SLA 达标率。
        /// </summary>
        [HttpGet("ticket-sla-summary")]
        public async Task<IActionResult> GetTicketSlaSummary()
        {
            var now =
                DateTime.UtcNow;


            /*
             * ==========================================
             * 1. 查询真正拥有 SLA 快照的工单
             * ==========================================
             */
            var tickets =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.SlaAppliedAt.HasValue
                            &&
                            x.SlaFirstResponseTargetMinutes.HasValue
                            &&
                            x.SlaResolutionTargetMinutes.HasValue
                            &&
                            x.SlaFirstResponseTargetMinutes.Value > 0
                            &&
                            x.SlaResolutionTargetMinutes.Value > 0
                    )

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.Status,

                            x.CreatedAt,

                            x.FirstResponseAt,

                            x.ResolvedAt,

                            x.ClosedAt,

                            FirstResponseTargetMinutes =
                                x.SlaFirstResponseTargetMinutes!.Value,

                            ResolutionTargetMinutes =
                                x.SlaResolutionTargetMinutes!.Value
                        }
                    )

                    .ToListAsync();


            /*
             * ==========================================
             * 首次响应 SLA
             * ==========================================
             */

            var responseEvaluatedCount = 0;

            var responseMetCount = 0;

            var responseBreachedCount = 0;

            /*
             * 当前还没有响应，
             * 并且已经超过 SLA 时间。
             */
            var currentResponseOverdueCount = 0;


            /*
             * ==========================================
             * 解决 SLA
             * ==========================================
             */

            var resolutionEvaluatedCount = 0;

            var resolutionMetCount = 0;

            var resolutionBreachedCount = 0;

            var currentResolutionOverdueCount = 0;


            foreach (var ticket in tickets)
            {
                /*
                 * 当前是否仍属于活跃工单。
                 */
                var isActive =
                    ticket.Status == "Pending"
                    ||
                    ticket.Status == "Processing";


                /*
                 * ======================================
                 * 首次响应 SLA 截止时间
                 * ======================================
                 */
                var responseDeadline =
                    ticket.CreatedAt.AddMinutes(
                        ticket.FirstResponseTargetMinutes
                    );


                if (ticket.FirstResponseAt.HasValue)
                {
                    /*
                     * 已经响应：
                     * 可以确定最终 SLA 结果。
                     */
                    responseEvaluatedCount++;


                    if (
                        ticket.FirstResponseAt.Value
                        <=
                        responseDeadline
                    )
                    {
                        responseMetCount++;
                    }
                    else
                    {
                        responseBreachedCount++;
                    }
                }
                else if (now > responseDeadline)
                {
                    /*
                     * 尚未响应，
                     * 但截止时间已经过去。
                     *
                     * 已经可以确定：
                     * SLA 失败。
                     */
                    responseEvaluatedCount++;

                    responseBreachedCount++;


                    /*
                     * 只有当前仍处于处理中，
                     * 才算“当前超时”。
                     *
                     * 已经关闭的历史异常数据
                     * 不再显示成当前待处理事项。
                     */
                    if (isActive)
                    {
                        currentResponseOverdueCount++;
                    }
                }


                /*
                 * ======================================
                 * 解决 SLA
                 * ======================================
                 *
                 * 正常使用 ResolvedAt。
                 *
                 * 如果某张工单直接关闭，
                 * ResolvedAt 意外为空，
                 * 则使用 ClosedAt 作为兜底。
                 *
                 * 防止 Closed 工单永远被认为
                 * “还没解决”。
                 */
                var effectiveResolvedAt =
                    ticket.ResolvedAt
                    ??
                    ticket.ClosedAt;


                var resolutionDeadline =
                    ticket.CreatedAt.AddMinutes(
                        ticket.ResolutionTargetMinutes
                    );


                if (effectiveResolvedAt.HasValue)
                {
                    resolutionEvaluatedCount++;


                    if (
                        effectiveResolvedAt.Value
                        <=
                        resolutionDeadline
                    )
                    {
                        resolutionMetCount++;
                    }
                    else
                    {
                        resolutionBreachedCount++;
                    }
                }
                else if (now > resolutionDeadline)
                {
                    resolutionEvaluatedCount++;

                    resolutionBreachedCount++;


                    if (isActive)
                    {
                        currentResolutionOverdueCount++;
                    }
                }
            }


            /*
             * ==========================================
             * 计算达标率
             * ==========================================
             *
             * 注意：
             *
             * 尚未到截止时间、而且尚未产生结果的工单，
             * 不进入分母。
             *
             * 否则刚创建5分钟的新工单
             * 会把 SLA 达标率错误拉低。
             */

            double? responseComplianceRate =
                null;


            if (responseEvaluatedCount > 0)
            {
                responseComplianceRate =
                    Math.Round(
                        responseMetCount
                        *
                        100.0
                        /
                        responseEvaluatedCount,

                        1
                    );
            }


            double? resolutionComplianceRate =
                null;


            if (resolutionEvaluatedCount > 0)
            {
                resolutionComplianceRate =
                    Math.Round(
                        resolutionMetCount
                        *
                        100.0
                        /
                        resolutionEvaluatedCount,

                        1
                    );
            }


            return Ok(
                new
                {
                    /*
                     * 拥有 SLA 快照的工单数量。
                     */
                    slaTicketCount =
                        tickets.Count,


                    /*
                     * 首次响应。
                     */
                    responseEvaluatedCount,

                    responseMetCount,

                    responseBreachedCount,

                    currentResponseOverdueCount,

                    responseComplianceRate,


                    /*
                     * 解决 SLA。
                     */
                    resolutionEvaluatedCount,

                    resolutionMetCount,

                    resolutionBreachedCount,

                    currentResolutionOverdueCount,

                    resolutionComplianceRate
                }
            );
        }

        /// <summary>
        /// 当前 SLA 已超时工单。
        ///
        /// GET:
        /// /api/dashboard/ticket-sla-overdue
        /// </summary>
        [HttpGet("ticket-sla-overdue")]
        public async Task<IActionResult> GetTicketSlaOverdue(
            [FromQuery] int take = 20)
        {
            /*
             * 限制返回数量，
             * 防止以后工单很多时一次返回太多。
             */
            if (take < 1)
            {
                take = 20;
            }


            if (take > 100)
            {
                take = 100;
            }


            var now =
                DateTime.UtcNow;


            /*
             * ==========================================
             * 只查询当前还需要处理的工单
             * ==========================================
             */
            var tickets =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .Where(
                        x =>
                            (
                                x.Status == "Pending"
                                ||
                                x.Status == "Processing"
                            )
                            &&
                            x.SlaAppliedAt.HasValue
                            &&
                            x.SlaFirstResponseTargetMinutes.HasValue
                            &&
                            x.SlaResolutionTargetMinutes.HasValue
                    )

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.TicketNo,

                            x.Title,

                            x.Priority,

                            x.Status,

                            x.CreatedAt,

                            x.FirstResponseAt,

                            x.ResolvedAt,

                            x.ClosedAt,

                            slaPriority =
                                x.SlaPriority,

                            firstResponseTargetMinutes =
                                x.SlaFirstResponseTargetMinutes!.Value,

                            resolutionTargetMinutes =
                                x.SlaResolutionTargetMinutes!.Value,

                            customerName =
                                x.Customer.Name,

                            softwareName =
                                x.Software.Name,

                            assignedToName =
                                x.AssignedToUser == null
                                    ? null
                                    : x.AssignedToUser.DisplayName
                        }
                    )

                    .ToListAsync();


            /*
             * ==========================================
             * 在内存中计算 Deadline
             * ==========================================
             *
             * 当前项目规模下这样更直观，
             * 也避免复杂 DateTime SQL 转换问题。
             */
            var overdueTickets =
                tickets

                    .Select(
                        ticket =>
                        {
                            var responseDeadline =
                                ticket.CreatedAt.AddMinutes(
                                    ticket.firstResponseTargetMinutes
                                );


                            var resolutionDeadline =
                                ticket.CreatedAt.AddMinutes(
                                    ticket.resolutionTargetMinutes
                                );


                            /*
                             * 没有首次响应，
                             * 并且已经超过响应截止时间。
                             */
                            var responseOverdue =
                                !ticket.FirstResponseAt.HasValue
                                &&
                                now > responseDeadline;


                            /*
                             * 当前仍未解决，
                             * 并且已经超过解决截止时间。
                             */
                            var resolutionOverdue =
                                !ticket.ResolvedAt.HasValue
                                &&
                                !ticket.ClosedAt.HasValue
                                &&
                                now > resolutionDeadline;


                            /*
                             * 超时多少分钟。
                             *
                             * 没超时返回0。
                             */
                            var responseOverdueMinutes =
                                responseOverdue
                                    ? Math.Round(
                                        (
                                            now
                                            -
                                            responseDeadline
                                        )
                                        .TotalMinutes,
                                        1
                                    )
                                    : 0;


                            var resolutionOverdueMinutes =
                                resolutionOverdue
                                    ? Math.Round(
                                        (
                                            now
                                            -
                                            resolutionDeadline
                                        )
                                        .TotalMinutes,
                                        1
                                    )
                                    : 0;


                            return new
                            {
                                ticket.Id,

                                ticket.TicketNo,

                                ticket.Title,

                                ticket.Priority,

                                ticket.Status,

                                ticket.customerName,

                                ticket.softwareName,

                                ticket.assignedToName,

                                ticket.CreatedAt,

                                ticket.FirstResponseAt,

                                ticket.slaPriority,

                                ticket.firstResponseTargetMinutes,

                                ticket.resolutionTargetMinutes,

                                responseDeadline,

                                resolutionDeadline,

                                responseOverdue,

                                resolutionOverdue,

                                responseOverdueMinutes,

                                resolutionOverdueMinutes,

                                /*
                                 * 用来统一排序：
                                 * 谁超时最严重谁在最前面。
                                 */
                                maxOverdueMinutes =
                                    Math.Max(
                                        responseOverdueMinutes,
                                        resolutionOverdueMinutes
                                    )
                            };
                        }
                    )

                    /*
                     * 只保留真正超时的。
                     */
                    .Where(
                        x =>
                            x.responseOverdue
                            ||
                            x.resolutionOverdue
                    )

                    /*
                     * 最严重的排最前。
                     */
                    .OrderByDescending(
                        x =>
                            x.maxOverdueMinutes
                    )

                    .Take(take)

                    .ToList();


            return Ok(overdueTickets);
        }


        /// <summary>
        /// 当前即将 SLA 超时的工单。
        ///
        /// GET:
        /// /api/dashboard/ticket-sla-warning
        ///
        /// 说明：
        ///
        /// SLA 截止时间：
        ///     使用 Ticket 创建时保存的 SLA 快照。
        ///
        /// 提前预警时间：
        ///     使用当前 TicketSlaRule.WarningBeforeMinutes。
        ///
        /// 这样管理员调整预警窗口后可以立即生效，
        /// 但不会改变历史 SLA 截止时间。
        /// </summary>
        [HttpGet("ticket-sla-warning")]
        public async Task<IActionResult> GetTicketSlaWarning(
            [FromQuery] int take = 20)
        {
            if (take < 1)
            {
                take = 20;
            }

            if (take > 100)
            {
                take = 100;
            }


            var now =
                DateTime.UtcNow;


            /*
             * ==========================================
             * 1. 读取当前启用的 SLA 预警规则
             * ==========================================
             *
             * WarningBeforeMinutes = 0
             * 表示该优先级不启用黄色预警。
             */
            var warningRules =
                await _dbContext.TicketSlaRules

                    .AsNoTracking()

                    .Where(
                        x =>
                            x.IsEnabled
                            &&
                            x.WarningBeforeMinutes > 0
                    )

                    .Select(
                        x => new
                        {
                            x.Priority,

                            x.WarningBeforeMinutes
                        }
                    )

                    .ToListAsync();


            /*
             * 没有任何预警规则，
             * 直接返回空数组。
             */
            if (warningRules.Count == 0)
            {
                return Ok(
                    Array.Empty<object>()
                );
            }


            /*
             * Priority -> WarningBeforeMinutes
             *
             * 忽略大小写。
             */
            var warningRuleMap =
                warningRules.ToDictionary(
                    x => x.Priority,

                    x => x.WarningBeforeMinutes,

                    StringComparer.OrdinalIgnoreCase
                );


            /*
             * ==========================================
             * 2. 查询当前活跃工单
             * ==========================================
             *
             * 只查：
             *
             * Pending
             * Processing
             *
             * 并且创建时确实应用过 SLA。
             */
            var tickets =
                await _dbContext.Tickets

                    .AsNoTracking()

                    .Where(
                        x =>
                            (
                                x.Status == "Pending"
                                ||
                                x.Status == "Processing"
                            )
                            &&
                            x.SlaAppliedAt.HasValue
                            &&
                            x.SlaPriority != null
                            &&
                            x.SlaFirstResponseTargetMinutes.HasValue
                            &&
                            x.SlaResolutionTargetMinutes.HasValue
                    )

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.TicketNo,

                            x.Title,

                            x.Priority,

                            x.Status,

                            x.CreatedAt,

                            x.FirstResponseAt,

                            x.ResolvedAt,

                            x.ClosedAt,

                            slaPriority =
                                x.SlaPriority!,

                            firstResponseTargetMinutes =
                                x.SlaFirstResponseTargetMinutes!.Value,

                            resolutionTargetMinutes =
                                x.SlaResolutionTargetMinutes!.Value,

                            customerName =
                                x.Customer.Name,

                            softwareName =
                                x.Software.Name,

                            assignedToName =
                                x.AssignedToUser == null
                                    ? null
                                    : x.AssignedToUser.DisplayName
                        }
                    )

                    .ToListAsync();


            /*
             * ==========================================
             * 3. 计算黄色预警
             * ==========================================
             */
            var warningTickets =
                tickets

                    .Select(
                        ticket =>
                        {
                            /*
                             * 当前优先级是否仍配置了预警策略。
                             */
                            if (
                                !warningRuleMap.TryGetValue(
                                    ticket.slaPriority,
                                    out var warningBeforeMinutes
                                )
                            )
                            {
                                return null;
                            }


                            /*
                             * ==================================
                             * 首次响应 SLA
                             * ==================================
                             */
                            var responseDeadline =
                                ticket.CreatedAt.AddMinutes(
                                    ticket.firstResponseTargetMinutes
                                );


                            var responseWarningStart =
                                responseDeadline.AddMinutes(
                                    -warningBeforeMinutes
                                );


                            /*
                             * 必须同时满足：
                             *
                             * 1. 还没有首次响应
                             * 2. 已进入预警窗口
                             * 3. 尚未真正超时
                             */
                            var responseWarning =
                                !ticket.FirstResponseAt.HasValue
                                &&
                                now >= responseWarningStart
                                &&
                                now <= responseDeadline;


                            /*
                             * ==================================
                             * 解决 SLA
                             * ==================================
                             */
                            var resolutionDeadline =
                                ticket.CreatedAt.AddMinutes(
                                    ticket.resolutionTargetMinutes
                                );


                            var resolutionWarningStart =
                                resolutionDeadline.AddMinutes(
                                    -warningBeforeMinutes
                                );


                            /*
                             * 当前工单仍未解决，
                             * 并且已经进入解决预警窗口。
                             */
                            var resolutionWarning =
                                !ticket.ResolvedAt.HasValue
                                &&
                                !ticket.ClosedAt.HasValue
                                &&
                                now >= resolutionWarningStart
                                &&
                                now <= resolutionDeadline;


                            /*
                             * 两种都没有进入黄色预警，
                             * 这张工单不返回。
                             */
                            if (
                                !responseWarning
                                &&
                                !resolutionWarning
                            )
                            {
                                return null;
                            }


                            /*
                             * 剩余时间。
                             */
                            double? responseRemainingMinutes =
                                responseWarning
                                    ? Math.Round(
                                        (
                                            responseDeadline
                                            -
                                            now
                                        )
                                        .TotalMinutes,
                                        1
                                    )
                                    : null;


                            double? resolutionRemainingMinutes =
                                resolutionWarning
                                    ? Math.Round(
                                        (
                                            resolutionDeadline
                                            -
                                            now
                                        )
                                        .TotalMinutes,
                                        1
                                    )
                                    : null;


                            /*
                             * 如果一张工单同时存在：
                             *
                             * 响应即将超时
                             * 解决即将超时
                             *
                             * 用剩余时间更少的那个
                             * 作为排序依据。
                             */
                            var remainingValues =
                                new[]
                                {
                            responseRemainingMinutes,
                            resolutionRemainingMinutes
                                }

                                .Where(
                                    x => x.HasValue
                                )

                                .Select(
                                    x => x!.Value
                                )

                                .ToList();


                            var minRemainingMinutes =
                                remainingValues.Min();


                            return new
                            {
                                ticket.Id,

                                ticket.TicketNo,

                                ticket.Title,

                                ticket.Priority,

                                ticket.Status,

                                ticket.customerName,

                                ticket.softwareName,

                                ticket.assignedToName,

                                ticket.CreatedAt,

                                ticket.FirstResponseAt,

                                ticket.slaPriority,

                                ticket.firstResponseTargetMinutes,

                                ticket.resolutionTargetMinutes,

                                warningBeforeMinutes,

                                responseDeadline,

                                resolutionDeadline,

                                responseWarning,

                                resolutionWarning,

                                responseRemainingMinutes,

                                resolutionRemainingMinutes,

                                minRemainingMinutes
                            };
                        }
                    )

                    /*
                     * 去掉没有预警的工单。
                     */
                    .Where(
                        x => x != null
                    )

                    /*
                     * 剩余时间越短，
                     * 风险越高，
                     * 越靠前。
                     */
                    .OrderBy(
                        x =>
                            x!.minRemainingMinutes
                    )

                    .Take(take)

                    .ToList();


            return Ok(
                warningTickets
            );
        }
    }
}