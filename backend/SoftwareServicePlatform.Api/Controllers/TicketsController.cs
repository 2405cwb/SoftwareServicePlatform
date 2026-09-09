using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Dtos.Tickets;
using SoftwareServicePlatform.Api.Models;
using System.Security.Claims;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 工单管理接口。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TicketsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;


        public TicketsController(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        /// <summary>
        /// 客户创建工单。
        ///
        /// POST /api/tickets
        ///
        /// 当前第一版只允许 Customer 用户自己创建工单。
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CreateTicket(
            CreateTicketRequest request)
        {
            /*
             * ==========================================
             * 1. 检查标题
             * ==========================================
             */

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(
                    "请输入工单标题"
                );
            }


            /*
             * 防止标题过长。
             */
            if (request.Title.Trim().Length > 200)
            {
                return BadRequest(
                    "工单标题不能超过200个字符"
                );
            }


            /*
             * ==========================================
             * 2. 检查问题描述
             * ==========================================
             */

            if (string.IsNullOrWhiteSpace(
                    request.Description))
            {
                return BadRequest(
                    "请输入问题描述"
                );
            }


            /*
             * ==========================================
             * 3. 检查软件ID
             * ==========================================
             */

            if (request.SoftwareId <= 0)
            {
                return BadRequest(
                    "请选择发生问题的软件"
                );
            }


            /*
             * ==========================================
             * 4. 检查优先级
             * ==========================================
             */

            var allowedPriorities =
                new[]
                {
                    "Low",
                    "Normal",
                    "High",
                    "Urgent"
                };


            if (!allowedPriorities.Contains(
                    request.Priority))
            {
                return BadRequest(
                    "工单优先级无效"
                );
            }


            /*
             * ==========================================
             * 5. 从 JWT 获取当前用户ID
             * ==========================================
             *
             * 登录的时候，
             * 我们已经把 User.Id 放到了：
             *
             * ClaimTypes.NameIdentifier
             */

            var userIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );


            if (!int.TryParse(
                    userIdText,
                    out var currentUserId))
            {
                return Unauthorized(
                    "无法获取当前登录用户"
                );
            }


            /*
             * ==========================================
             * 6. 从 JWT 获取 CustomerId
             * ==========================================
             *
             * Customer 登录时 JWT 中包含：
             *
             * customerId
             */

            var customerIdText =
                User.FindFirstValue(
                    "customerId"
                );


            if (!int.TryParse(
                    customerIdText,
                    out var customerId))
            {
                return Unauthorized(
                    "当前用户未绑定客户"
                );
            }


            /*
             * ==========================================
             * 7. 再查询一次数据库中的当前用户
             * ==========================================
             *
             * 为什么 JWT 已经有用户信息，
             * 这里还要查询数据库？
             *
             * 因为 JWT 是登录时签发的。
             *
             * 如果管理员后来把用户停用了，
             * JWT 里面原来的信息还在。
             *
             * 所以创建工单这种真正修改数据库的操作，
             * 再检查一次当前用户状态会更安全。
             */

            var currentUser =
                await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == currentUserId
                    );


            if (currentUser == null)
            {
                return Unauthorized(
                    "当前用户不存在"
                );
            }


            if (!currentUser.IsEnabled)
            {
                return Unauthorized(
                    "当前用户已停用"
                );
            }


            /*
             * 当前接口只能由 Customer 创建。
             *
             * 虽然上面已经有：
             *
             * [Authorize(Roles = "Customer")]
             *
             * 这里再次检查数据库里的真实角色，
             * 防止旧 JWT 与数据库当前状态不一致。
             */

            if (currentUser.Role != "Customer")
            {
                return Forbid();
            }


            /*
             * 当前用户数据库中的 CustomerId
             * 必须和 JWT 中一致。
             */

            if (!currentUser.CustomerId.HasValue
                ||
                currentUser.CustomerId.Value != customerId)
            {
                return Unauthorized(
                    "当前用户客户信息已经发生变化，请重新登录"
                );
            }


            /*
             * ==========================================
             * 8. 检查客户是否仍然存在并启用
             * ==========================================
             */

            var customer =
                await _dbContext.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == customerId
                    );


            if (customer == null)
            {
                return BadRequest(
                    "所属客户不存在"
                );
            }


            if (!customer.IsEnabled)
            {
                return BadRequest(
                    "所属客户已停用"
                );
            }


            /*
             * ==========================================
             * 9. 检查软件是否存在
             * ==========================================
             */

            var software =
                await _dbContext.Softwares
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == request.SoftwareId
                    );


            if (software == null)
            {
                return BadRequest(
                    "所选软件不存在"
                );
            }


            /*
             * ==========================================
             * 10. 最关键的权限检查
             * ==========================================
             *
             * 客户只能给自己已经绑定的软件提交工单。
             *
             * 例如：
             *
             * CustomerId = 3
             *
             * 只绑定：
             *
             * SoftwareId = 5
             *
             * 那么客户不能自己把请求改成：
             *
             * SoftwareId = 8
             *
             * 去给其他软件创建工单。
             */

            var hasSoftware =
                await _dbContext.CustomerSoftwares
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.CustomerId == customerId
                            &&
                            x.SoftwareId == request.SoftwareId
                            &&
                            x.IsEnabled
                    );


            if (!hasSoftware)
            {
                return BadRequest(
                    "当前客户未绑定该软件，不能提交工单"
                );
            }


            /*
             * ==========================================
             * 11. 创建时间
             * ==========================================
             */

            var now =
                DateTime.UtcNow;


            /*
             * ==========================================
             * 12. 创建 Ticket
             * ==========================================
             *
             * TicketNo 暂时先放一个绝对唯一的临时值。
             *
             * 为什么？
             *
             * 因为我们希望正式工单号包含数据库 Id：
             *
             * TK20260909000001
             *
             * 但 Id 是数据库插入以后才会生成。
             *
             * 所以先保存一次，
             * 拿到 Id，
             * 然后再生成正式 TicketNo。
             */

            var ticket =
                new Ticket
                {
                    /*
                     * 临时工单编号。
                     *
                     * TicketNo 有唯一索引，
                     * 所以不能所有新工单都先写空字符串。
                     */
                    TicketNo =
                        $"TMP-{Guid.NewGuid():N}",


                    Title =
                        request.Title.Trim(),


                    Description =
                        request.Description.Trim(),


                    Priority =
                        request.Priority,


                    /*
                     * 新工单默认待处理。
                     */
                    Status =
                        "Pending",


                    /*
                     * 这两个值全部来自服务器身份，
                     * 不是来自前端。
                     */
                    CustomerId =
                        customerId,


                    CreatedByUserId =
                        currentUserId,


                    /*
                     * 软件ID虽然来自前端，
                     * 但前面已经验证客户确实拥有它。
                     */
                    SoftwareId =
                        request.SoftwareId,


                    /*
                     * 新工单刚创建时，
                     * 暂时还没有分配处理人。
                     */
                    AssignedToUserId =
                        null,


                    CreatedAt =
                        now,


                    UpdatedAt =
                        now,


                    ResolvedAt =
                        null
                };


            /*
             * ==========================================
             * 13. 第一次保存
             * ==========================================
             *
             * 保存以后数据库会自动生成：
             *
             * ticket.Id
             */

            _dbContext.Tickets.Add(
                ticket
            );


            await _dbContext.SaveChangesAsync();


            /*
             * ==========================================
             * 14. 生成正式工单编号
             * ==========================================
             *
             * 例如：
             *
             * 日期：
             * 2026-09-09
             *
             * Ticket Id：
             * 17
             *
             * 最终：
             *
             * TK20260909000017
             *
             * D6 表示：
             *
             * 17
             * ↓
             * 000017
             */

            ticket.TicketNo =
                $"TK{now:yyyyMMdd}{ticket.Id:D6}";


            ticket.UpdatedAt =
                DateTime.UtcNow;


            /*
             * ==========================================
             * 15. 第二次保存正式工单编号
             * ==========================================
             */

            await _dbContext.SaveChangesAsync();


            /*
             * ==========================================
             * 16. 返回前端
             * ==========================================
             */

            return StatusCode(
                StatusCodes.Status201Created,
                new
                {
                    message =
                        "工单创建成功",

                    ticket.Id,

                    ticket.TicketNo,

                    ticket.Title,

                    ticket.Description,

                    ticket.Status,

                    ticket.Priority,

                    ticket.CustomerId,

                    customerName =
                        customer.Name,

                    ticket.SoftwareId,

                    softwareName =
                        software.Name,

                    ticket.CreatedByUserId,

                    createdByName =
                        currentUser.DisplayName,

                    ticket.AssignedToUserId,

                    ticket.CreatedAt
                }
            );
        }
    }
}