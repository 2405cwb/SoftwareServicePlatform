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
    }
}