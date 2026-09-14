using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 工单 SLA 规则管理。
    ///
    /// 当前 SLA 规则只允许 Admin 管理。
    ///
    /// 支持：
    ///
    /// GET
    ///     查询全部 SLA 配置
    ///
    /// PUT
    ///     新增或修改某个优先级规则
    ///
    /// 不提供物理删除：
    /// 如果某条规则暂时不用，
    /// 设置 IsEnabled = false。
    /// </summary>
    [ApiController]
    [Route("api/ticket-sla-rules")]
    [Authorize(Roles = "Admin")]
    public class TicketSlaRulesController : ControllerBase
    {
        private readonly AppDbContext _dbContext;


        /*
         * 系统当前允许的四种工单优先级。
         *
         * SLA 配置不能创建其他字符串。
         */
        private static readonly string[] AllowedPriorities =
        {
            "Low",
            "Normal",
            "High",
            "Urgent"
        };


        public TicketSlaRulesController(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        /// <summary>
        /// 查询全部 SLA 规则。
        ///
        /// GET:
        ///
        /// /api/ticket-sla-rules
        ///
        /// 即使数据库中某个优先级还没有配置，
        /// 也会返回这一项，
        /// configured = false。
        ///
        /// 这样前端永远都能显示固定四行：
        ///
        /// Low
        /// Normal
        /// High
        /// Urgent
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRules()
        {
            var rules =
                await _dbContext.TicketSlaRules

                    .AsNoTracking()

                    .ToListAsync();


            /*
             * 使用忽略大小写的字典，
             * 增强旧数据兼容性。
             */
            var ruleMap =
                rules.ToDictionary(
                    x => x.Priority,
                    StringComparer.OrdinalIgnoreCase
                );


            /*
             * 不直接返回数据库列表。
             *
             * 而是按照系统固定顺序返回四种优先级。
             */
            var result =
                AllowedPriorities

                    .Select(
                        priority =>
                        {
                            ruleMap.TryGetValue(
                                priority,
                                out var rule
                            );


                            return new
                            {
                                /*
                                 * 数据库是否已经配置。
                                 */
                                configured =
                                    rule != null,


                                priority,


                                /*
                                 * 未配置时返回 null。
                                 *
                                 * 前端显示：
                                 *
                                 * 未配置
                                 */
                                firstResponseTargetMinutes =
                                    rule == null
                                        ? (int?)null
                                        : rule.FirstResponseTargetMinutes,


                                resolutionTargetMinutes =
                                    rule == null
                                        ? (int?)null
                                        : rule.ResolutionTargetMinutes,

                                warningBeforeMinutes = 
                                 rule == null
                                     ? (int?)null
                                     : rule.WarningBeforeMinutes,
                                                             /*
                                 * 尚未配置时默认 false，
                                 * 因为不能让不存在的规则
                                 * 自动参与 SLA 判断。
                                 */
                                isEnabled =
                                    rule?.IsEnabled
                                    ?? false,


                                updatedAt =
                                    rule?.UpdatedAt
                            };
                        }
                    )

                    .ToList();


            return Ok(result);
        }


        /// <summary>
        /// 新增或修改指定优先级的 SLA 规则。
        ///
        /// PUT:
        ///
        /// /api/ticket-sla-rules/High
        ///
        /// Body:
        ///
        /// {
        ///     "firstResponseTargetMinutes": 120,
        ///     "resolutionTargetMinutes": 1440,
        ///     "isEnabled": true
        /// }
        ///
        /// 如果 High 不存在：
        ///     创建
        ///
        /// 如果 High 已经存在：
        ///     修改
        /// </summary>
        [HttpPut("{priority}")]
        public async Task<IActionResult> SaveRule(
            string priority,
            [FromBody] SaveTicketSlaRuleRequest request)
        {
            /*
             * ==========================================
             * 1. 标准化 Priority
             * ==========================================
             *
             * 即使前端传：
             *
             * high
             * HIGH
             * High
             *
             * 最终数据库统一保存：
             *
             * High
             */
            var normalizedPriority =
                AllowedPriorities

                    .FirstOrDefault(
                        x =>
                            string.Equals(
                                x,
                                priority,
                                StringComparison.OrdinalIgnoreCase
                            )
                    );


            if (normalizedPriority == null)
            {
                return BadRequest(
                    "不支持的工单优先级"
                );
            }


            /*
             * ==========================================
             * 2. 时间参数检查
             * ==========================================
             */
            if (
                request.FirstResponseTargetMinutes
                <= 0
            )
            {
                return BadRequest(
                    "首次响应目标必须大于0分钟"
                );
            }


            if (
                request.ResolutionTargetMinutes
                <= 0
            )
            {
                return BadRequest(
                    "解决目标必须大于0分钟"
                );
            }

            if (
    request.WarningBeforeMinutes
    <
    0
)
            {
                return BadRequest(
                    "预警时间不能小于0分钟"
                );
            }

            /*
 * 预警时间不能大于解决 SLA 本身。
 *
 * 例如：
 *
 * 解决 SLA = 8小时
 * 预警 = 提前12小时
 *
 * 显然不合理。
 */
            if (
                request.WarningBeforeMinutes
                >
                request.ResolutionTargetMinutes
            )
            {
                return BadRequest(
                    "预警时间不能大于解决目标时间"
                );
            }
            /*
             * 一般情况下：
             *
             * 解决目标
             * 应该大于或等于
             * 首次响应目标。
             *
             * 否则：
             *
             * 首次响应要求4小时，
             * 但解决要求2小时
             *
             * 业务逻辑明显不合理。
             */
            if (
                request.ResolutionTargetMinutes
                <
                request.FirstResponseTargetMinutes
            )
            {
                return BadRequest(
                    "解决目标不能小于首次响应目标"
                );
            }


            /*
             * ==========================================
             * 3. 查询现有规则
             * ==========================================
             */
            var rule =
                await _dbContext.TicketSlaRules

                    .FirstOrDefaultAsync(
                        x =>
                            x.Priority
                            ==
                            normalizedPriority
                    );


            var now =
                DateTime.UtcNow;


            /*
             * ==========================================
             * 4. 不存在则创建
             * ==========================================
             */
            if (rule == null)
            {
                rule =
                    new TicketSlaRule
                    {
                        Priority =
                            normalizedPriority,

                        FirstResponseTargetMinutes =
                            request.FirstResponseTargetMinutes,

                        ResolutionTargetMinutes =
                            request.ResolutionTargetMinutes,
                        WarningBeforeMinutes =
            request.WarningBeforeMinutes,

                        IsEnabled =
                            request.IsEnabled,

                        CreatedAt =
                            now,

                        UpdatedAt =
                            now
                    };


                _dbContext.TicketSlaRules.Add(
                    rule
                );
            }
            else
            {
                /*
                 * ======================================
                 * 5. 已存在则修改
                 * ======================================
                 */
                rule.FirstResponseTargetMinutes =
                    request.FirstResponseTargetMinutes;


                rule.ResolutionTargetMinutes =
                    request.ResolutionTargetMinutes;


                rule.IsEnabled =
                    request.IsEnabled;


                rule.UpdatedAt =
                    now;
            }


            await _dbContext.SaveChangesAsync();


            /*
             * ==========================================
             * 6. 返回保存后的规则
             * ==========================================
             */
            return Ok(
                new
                {
                    rule.Id,

                    rule.Priority,

                    rule.FirstResponseTargetMinutes,

                    rule.ResolutionTargetMinutes,
                    rule.WarningBeforeMinutes,
                    rule.IsEnabled,

                    rule.CreatedAt,

                    rule.UpdatedAt
                }
            );
        }
    }


    /// <summary>
    /// 保存 SLA 规则请求。
    /// </summary>
    public class SaveTicketSlaRuleRequest
    {
        /// <summary>
        /// 首次响应目标，单位：分钟。
        /// </summary>
        public int FirstResponseTargetMinutes { get; set; }


        /// <summary>
        /// 解决目标，单位：分钟。
        /// </summary>
        public int ResolutionTargetMinutes { get; set; }

        /// <summary>
        /// SLA 截止前多少分钟开始预警。
        ///
        /// 0 表示关闭即将超时预警。
        /// </summary>
        public int WarningBeforeMinutes { get; set; }
        /// <summary>
        /// 是否启用 SLA 判断。
        /// </summary>
        public bool IsEnabled { get; set; }
    }
}