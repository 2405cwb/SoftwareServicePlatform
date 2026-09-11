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

        /// <summary>
        /// 查询当前用户有权限看到的工单。
        ///
        /// GET /api/tickets
        ///
        /// 不同角色看到的数据范围不同：
        ///
        /// Admin
        ///     查看全部工单
        ///
        /// Support
        ///     查看全部工单
        ///
        /// Developer
        ///     只查看分配给自己的工单
        ///
        /// Customer
        ///     只查看自己所属客户的工单
        /// </summary>
        [HttpGet]
        [Authorize(
            Roles = "Admin,Support,Developer,Customer"
        )]
        public async Task<IActionResult> GetTickets()
        {
            /*
             * ==========================================
             * 1. 从 JWT 获取当前用户ID
             * ==========================================
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
             * 2. 查询当前用户最新数据库状态
             * ==========================================
             *
             * 不完全相信 JWT 中登录时保存的旧状态。
             *
             * 例如：
             *
             * 用户登录之后，
             * 管理员把这个账号停用了。
             *
             * JWT 可能还没有过期，
             * 但数据库中的 IsEnabled 已经变成 false。
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
             * ==========================================
             * 3. 创建最基础的 Ticket 查询
             * ==========================================
             *
             * IQueryable 可以理解成：
             *
             * 现在只是在“组装SQL查询条件”，
             * 还没有真正去 PostgreSQL 查询。
             *
             * 直到后面的 ToListAsync()
             * 才真正执行 SQL。
             */

            IQueryable<Ticket> query =
                _dbContext.Tickets
                    .AsNoTracking();


            /*
             * ==========================================
             * 4. 根据角色限制查询范围
             * ==========================================
             */

            switch (currentUser.Role)
            {
                /*
                 * ------------------------------
                 * Admin
                 * ------------------------------
                 *
                 * 管理员可以看到所有工单。
                 *
                 * 所以这里什么条件都不用加。
                 */
                case "Admin":
                    break;


                /*
                 * ------------------------------
                 * Support
                 * ------------------------------
                 *
                 * 售后需要处理客户问题，
                 * 所以也可以看到所有工单。
                 */
                case "Support":
                    break;


                /*
                 * ------------------------------
                 * Developer
                 * ------------------------------
                 *
                 * 开发人员目前只看到
                 * 已经分配给自己的工单。
                 */
                case "Developer":

                    query =
                        query.Where(
                            x =>
                                x.AssignedToUserId
                                ==
                                currentUser.Id
                        );

                    break;


                /*
                 * ------------------------------
                 * Customer
                 * ------------------------------
                 *
                 * 客户只能看到自己公司的工单。
                 */
                case "Customer":

                    /*
                     * Customer 用户必须绑定 Customer。
                     */
                    if (!currentUser.CustomerId.HasValue)
                    {
                        return Unauthorized(
                            "当前用户未绑定客户"
                        );
                    }


                    /*
                     * 再确认客户仍然存在并且启用。
                     */
                    var customerExists =
                        await _dbContext.Customers
                            .AsNoTracking()
                            .AnyAsync(
                                x =>
                                    x.Id ==
                                    currentUser.CustomerId.Value
                                    &&
                                    x.IsEnabled
                            );


                    if (!customerExists)
                    {
                        return Unauthorized(
                            "所属客户不存在或已停用"
                        );
                    }


                    /*
                     * 最关键的客户数据隔离：
                     *
                     * WHERE CustomerId =
                     * 当前登录用户所属 CustomerId
                     */
                    query =
                        query.Where(
                            x =>
                                x.CustomerId
                                ==
                                currentUser.CustomerId.Value
                        );

                    break;


                /*
                 * 其他角色全部拒绝。
                 *
                 * 比如当前 Sales 暂时没有工单权限。
                 */
                default:

                    return Forbid();
            }


            /*
             * ==========================================
             * 5. 查询工单数据
             * ==========================================
             *
             * 这里不直接：
             *
             * return Ok(ticket);
             *
             * 而是 Select 出前端真正需要的数据。
             *
             * 好处：
             *
             * 1. 不会把整个 EF 实体直接暴露出去
             * 2. 不会产生导航属性循环 JSON
             * 3. 前端拿到的数据结构更加稳定
             */

            var tickets =
                await query
                    /*
                     * 最新工单排最前面。
                     */
                    .OrderByDescending(
                        x => x.CreatedAt
                    )

                    .Select(
                        x => new
                        {
                            /*
                             * 工单基本信息
                             */
                            x.Id,

                            x.TicketNo,

                            x.Title,

                            x.Description,

                            x.Status,

                            x.Priority,


                            /*
                             * 客户信息
                             */
                            x.CustomerId,

                            CustomerName =
                                x.Customer.Name,


                            /*
                             * 软件信息
                             */
                            x.SoftwareId,

                            SoftwareName =
                                x.Software.Name,


                            /*
                             * 创建人信息
                             */
                            x.CreatedByUserId,

                            CreatedByName =
                                x.CreatedByUser.DisplayName,

                            CreatedByUsername =
                                x.CreatedByUser.Username,


                            /*
                             * 当前处理人。
                             *
                             * 新工单可能还没有处理人，
                             * 所以 AssignedToUser 可能为 null。
                             */
                            x.AssignedToUserId,

                            AssignedToName =
                                x.AssignedToUser == null
                                    ? null
                                    : x.AssignedToUser.DisplayName,


                            /*
                             * 时间
                             */
                            x.CreatedAt,

                            x.UpdatedAt,

                            x.ResolvedAt
                        }
                    )

                    /*
                     * 到这里才真正向 PostgreSQL
                     * 发起查询。
                     */
                    .ToListAsync();


            /*
             * ==========================================
             * 6. 返回工单列表
             * ==========================================
             */

            return Ok(
                tickets
            );
        }
        /// <summary>
        /// 分配工单。
        ///
        /// PUT /api/tickets/{id}/assign
        ///
        /// 只有：
        /// Admin
        /// Support
        ///
        /// 可以分配工单。
        /// </summary>
        [HttpPut("{id}/assign")]
        [Authorize(Roles = "Admin,Support,Developer")]
        public async Task<IActionResult> AssignTicket(
            int id,
            AssignTicketRequest request)
        {
            /*
             * ==========================================
             * 1. 检查工单ID
             * ==========================================
             */

            if (id <= 0)
            {
                return BadRequest(
                    "工单ID无效"
                );
            }


            /*
             * ==========================================
             * 2. 检查处理人ID
             * ==========================================
             */

            if (request.AssignedToUserId <= 0)
            {
                return BadRequest(
                    "请选择工单处理人"
                );
            }


            /*
             * ==========================================
             * 3. 查询工单
             * ==========================================
             *
             * 注意这里不能使用 AsNoTracking()。
             *
             * 因为等一下我们需要修改：
             *
             * AssignedToUserId
             * Status
             * UpdatedAt
             *
             * 并保存数据库。
             */

            var ticket =
                await _dbContext.Tickets
                    .FirstOrDefaultAsync(
                        x => x.Id == id
                    );


            if (ticket == null)
            {
                return NotFound(
                    "工单不存在"
                );
            }


            /*
             * ==========================================
             * 4. 已关闭的工单不能再分配
             * ==========================================
             */

            if (ticket.Status == "Closed")
            {
                return BadRequest(
                    "已关闭的工单不能重新分配"
                );
            }


            /*
             * 已经解决的工单也暂时不允许直接重新分配。
             *
             * 后面如果需要“重新打开工单”，
             * 我们会单独设计 Reopen 接口。
             */
            if (ticket.Status == "Resolved")
            {
                return BadRequest(
                    "已解决的工单不能直接重新分配"
                );
            }


            /*
             * ==========================================
             * 5. 查询准备分配给的员工
             * ==========================================
             */

            var assignedUser =
                await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                            request.AssignedToUserId
                    );


            if (assignedUser == null)
            {
                return BadRequest(
                    "所选处理人不存在"
                );
            }


            /*
             * ==========================================
             * 6. 检查处理人账号是否启用
             * ==========================================
             */

            if (!assignedUser.IsEnabled)
            {
                return BadRequest(
                    "所选处理人账号已停用"
                );
            }


            /*
             * ==========================================
             * 7. 检查处理人角色
             * ==========================================
             *
             * 当前第一版只允许：
             *
             * Support
             * Developer
             *
             * 作为工单处理人。
             *
             * Customer 显然不能处理工单。
             * Sales 当前也不参与技术工单处理。
             */

            var allowedRoles =
                new[]
                {
            "Support",
            "Developer"
                };


            if (!allowedRoles.Contains(
                    assignedUser.Role))
            {
                return BadRequest(
                    "工单只能分配给售后人员或开发人员"
                );
            }


            /*
             * ==========================================
             * 8. 修改工单处理人
             * ==========================================
             */

            ticket.AssignedToUserId =
                assignedUser.Id;


            /*
             * 工单只要被正式分配，
             * 就从：
             *
             * Pending
             *
             * 进入：
             *
             * Processing
             */
            ticket.Status =
                "Processing";


            /*
             * 更新时间。
             */
            ticket.UpdatedAt =
                DateTime.UtcNow;


            /*
             * ==========================================
             * 9. 保存数据库
             * ==========================================
             */

            await _dbContext.SaveChangesAsync();


            /*
             * ==========================================
             * 10. 查询关联信息
             * ==========================================
             *
             * 这里为了返回更友好的结果，
             * 顺手把 Customer 和 Software 名称取出来。
             */

            var customer =
                await _dbContext.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == ticket.CustomerId
                    );


            var software =
                await _dbContext.Softwares
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == ticket.SoftwareId
                    );


            /*
             * ==========================================
             * 11. 返回结果
             * ==========================================
             */

            return Ok(
                new
                {
                    message =
                        "工单分配成功",

                    ticket.Id,

                    ticket.TicketNo,

                    ticket.Title,

                    ticket.Status,

                    ticket.Priority,

                    ticket.CustomerId,

                    customerName =
                        customer?.Name
                        ?? string.Empty,

                    ticket.SoftwareId,

                    softwareName =
                        software?.Name
                        ?? string.Empty,

                    ticket.AssignedToUserId,

                    assignedToName =
                        assignedUser.DisplayName,

                    assignedToRole =
                        assignedUser.Role,

                    ticket.UpdatedAt
                }
            );
        }
    }
}