using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Dtos.Tickets;
using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services;
using SoftwareServicePlatform.Api.Services.ExternalNotifications;
using SoftwareServicePlatform.Api.Services.NotificationPolicies;
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
        private readonly AppDbContext
    _dbContext;


        /*
         * ==========================================
         * 旧站内通知服务
         * ==========================================
         *
         * 当前 TicketsController 中还有：
         *
         * 工单回复
         * 工单解决
         * 工单关闭
         * 工单重新打开
         *
         * 等旧通知尚未迁移。
         *
         * 所以现在不能删除。
         *
         * 等整个 Controller 的通知全部迁移完成后，
         * 再统一移除。
         */
        private readonly INotificationService
            _notificationService;


        /*
         * ==========================================
         * 新统一通知事件服务
         * ==========================================
         *
         * 新迁移的通知事件统一从这里发送。
         *
         * Controller 不再负责：
         *
         * 查询通知接收人
         * 判断是否发钉钉
         * 判断是否 @
         * 调用 SignalR
         *
         * 这些都由通知策略系统负责。
         */
        private readonly INotificationEventService
            _notificationEventService;


        public TicketsController(
            AppDbContext dbContext,
            INotificationService notificationService,
            INotificationEventService notificationEventService)
        {
            _dbContext =
                dbContext;

            _notificationService =
                notificationService;

            _notificationEventService =
                notificationEventService;
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
 * 查询当前优先级生效的 SLA 规则
 * ==========================================
 *
 * 注意：
 *
 * 这里只在“创建工单”这一刻读取一次。
 *
 * 后面 SLA 规则即使修改，
 * 也不会影响这张历史工单。
 */
            var slaRule =
                await _dbContext.TicketSlaRules

                    .AsNoTracking()

                    .FirstOrDefaultAsync(
                        x =>
                            x.Priority == request.Priority
                            &&
                            x.IsEnabled
                    );
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
 * ==========================================
 * SLA 快照
 * ==========================================
 */
                    SlaPriority =
    slaRule == null
        ? null
        : request.Priority,


                    SlaFirstResponseTargetMinutes =
    slaRule?.FirstResponseTargetMinutes,


                    SlaResolutionTargetMinutes =
    slaRule?.ResolutionTargetMinutes,


                    SlaAppliedAt =
    slaRule == null
        ? null
        : now,
                    /*
                    * 当前 CreateTicket 接口
                    * 只能由 Customer 在客户门户提交，
                    * 所以来源一定是 Portal。
                    */
                    Source = "Portal",

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


                    FirstResponseAt =
    null,


                    ResolvedAt =
    null,


                    ClosedAt =
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
     * 15. 保存正式工单编号
     * ==========================================
     *
     * 前面第一次 SaveChangesAsync()
     * 是为了取得数据库生成的 ticket.Id。
     *
     * 现在 TicketNo 已经根据 Id 生成完成，
     * 所以必须先把最终业务数据保存成功。
     *
     * 非常重要：
     *
     * NotificationEventService 后面会通过 TicketId
     * 再次从数据库读取：
     *
     * CustomerId
     * AssignedToUserId
     *
     * 因此必须：
     *
     * 先保存业务
     *     ↓
     * 再发布通知事件
     */
            await _dbContext.SaveChangesAsync();


            /*
             * ==========================================
             * 16. 发布“客户创建工单”通知事件
             * ==========================================
             *
             * 从这里开始，
             * CreateTicket 不再自己查询所有 Support。
             *
             * 数据库中的通知策略：
             *
             * EventKey：
             * Ticket.Created
             *
             * RecipientStrategy：
             * Support
             *
             * 所以 NotificationRecipientResolver
             * 会自动查询所有：
             *
             * IsEnabled = true
             * Role = Support
             *
             * 的用户。
             *
             *
             * 同样，
             * Controller 也不再决定：
             *
             * 是否发送钉钉
             * 是否发送站内通知
             * 是否 @
             *
             * 全部由：
             *
             * NotificationPolicies
             * NotificationPolicyChannels
             *
             * 控制。
             */
            await _notificationEventService.PublishAsync(
                new NotificationEventRequest
                {
                    /*
                     * 系统统一业务事件编码。
                     *
                     * 不再直接手写：
                     *
                     * "Ticket.Created"
                     */
                    EventKey =
                        NotificationEventKeys
                            .TicketCreated,


                    /*
                     * ==========================================
                     * 当前通知关联的业务对象
                     * ==========================================
                     *
                     * Resolver 通过 TicketId
                     * 可以继续获取：
                     *
                     * CustomerId
                     * AssignedToUserId
                     *
                     * 当前策略是 Support，
                     * 虽然暂时不需要这些字段，
                     * 但统一保持相同的上下文结构。
                     */
                    Context =
                        new NotificationRecipientContext
                        {
                            TicketId =
                                ticket.Id
                        },


                    /*
                     * ==========================================
                     * 通知标题
                     * ==========================================
                     */
                    Title =
                        $"收到新的客户工单：{ticket.TicketNo}",


                    /*
                     * ==========================================
                     * 通知正文
                     * ==========================================
                     *
                     * 继续复用你现在已经写好的方法。
                     *
                     * 通知内容属于业务表达，
                     * 暂时仍然由业务代码负责。
                     */
                    Content =
                        BuildTicketCreatedNotificationContent(
                            ticket,
                            customer.Name,
                            software.Name
                        ),


                    /*
                     * ==========================================
                     * Level
                     * ==========================================
                     *
                     * 这里故意不填写 Level。
                     *
                     * null：
                     * NotificationEventService 自动使用：
                     *
                     * NotificationPolicy.DefaultLevel
                     *
                     * 当前 Ticket.Created
                     * 默认就是 Info。
                     *
                     * 这样通知级别也开始逐步配置化。
                     */


                    /*
                     * 点击站内通知后，
                     * 直接进入这张工单。
                     */
                    TargetUrl =
                        $"/tickets?ticketId={ticket.Id}",


                    /*
                     * ==========================================
                     * 通知去重
                     * ==========================================
                     *
                     * 新架构不需要再写：
                     *
                     * ticket:15:created:support:3
                     * ticket:15:created:support:5
                     *
                     * 因为 Notification 表的唯一索引是：
                     *
                     * UserId + DedupKey
                     *
                     * 所以所有接收人都可以使用完全相同的：
                     *
                     * ticket:15:created
                     *
                     * UserId 不同，
                     * 数据库仍然允许每个人各保存一条。
                     */
                    DedupKey =
                        $"ticket:{ticket.Id}:created",


                    /*
                     * ==========================================
                     * 兼容现有 Notification.Type
                     * ==========================================
                     *
                     * 现有历史通知使用：
                     *
                     * TicketCreated
                     *
                     * 第一阶段继续保持，
                     * 避免同时修改前端。
                     */
                    NotificationType =
                        "TicketCreated"
                }
            );
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

                    ticket.SlaPriority,

                    ticket.SlaFirstResponseTargetMinutes,

                    ticket.SlaResolutionTargetMinutes,

                    ticket.SlaAppliedAt,

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


                            x.Source,

                            x.CreatedAt,

                            x.UpdatedAt,

                            x.FirstResponseAt,

                            x.ResolvedAt,

                            x.ClosedAt
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
 * 获取当前执行分配操作的用户ID
 * ==========================================
 *
 * 用于 TicketRecord.CreatedByUserId。
 */

            var currentUserIdText =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );


            if (!int.TryParse(
                    currentUserIdText,
                    out var currentUserId))
            {
                return Unauthorized(
                    "无法获取当前登录用户"
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
 * Developer 只能重新分配
 * 当前已经分配给自己的工单。
 *
 * 不能修改其他开发人员的工单。
 */
            var currentUser =
                await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == currentUserId
                            &&
                            x.IsEnabled
                    );


            if (currentUser == null)
            {
                return Unauthorized(
                    "当前用户不存在或已停用"
                );
            }


            if (currentUser.Role == "Developer")
            {
                if (ticket.AssignedToUserId
                    !=
                    currentUser.Id)
                {
                    return Forbid();
                }
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
 * 防止重复分配给当前处理人
 * ==========================================
 *
 * 例如：
 *
 * 当前处理人 = 张三
 * 新处理人   = 张三
 *
 * 这种操作没有产生任何真正业务变化，
 * 不应该：
 *
 * 生成 TicketRecord
 * 发送站内通知
 * 发送钉钉
 */
            if (ticket.AssignedToUserId ==
                assignedUser.Id)
            {
                return BadRequest(
                    "该用户已经是当前工单处理人，无需重复分配"
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
 * Developer 转交工单时，
 * 只能转给其他 Developer。
 *
 * Admin / Support 不受这个限制。
 */
            if (currentUser.Role == "Developer"
                &&
                assignedUser.Role != "Developer")
            {
                return BadRequest(
                    "开发人员只能将工单转交给其他开发人员"
                );
            }

            /*
    * ==========================================
    * 8. 修改工单处理人
    * ==========================================
    */

            var now =
                DateTime.UtcNow;


            /*
             * 先记住旧处理人。
             *
             * 后面如果是“重新分配”，
             * 时间线可以明确记录：
             *
             * 王工 → 李工
             */
            var oldAssignedToUserId =
                ticket.AssignedToUserId;

            /*
 * ==========================================
 * 判断本次属于第一次分配还是重新分配
 * ==========================================
 *
 * oldAssignedToUserId == null：
 *     Ticket.Assigned
 *
 * oldAssignedToUserId != null：
 *     Ticket.Reassigned
 */
            var isReassignment =
                oldAssignedToUserId.HasValue;
            /*
             * 设置新的处理人。
             */
            ticket.AssignedToUserId =
                assignedUser.Id;


            /*
             * 只要工单正式分配给处理人员，
             * 状态进入 Processing。
             */
            ticket.Status =
                "Processing";


            ticket.UpdatedAt =
                now;


            /*
             * ==========================================
             * 9. 生成分配历史记录
             * ==========================================
             */

            string assignContent;


            /*
             * 第一次分配：
             *
             * AssignedToUserId 原来为空。
             */
            if (!oldAssignedToUserId.HasValue)
            {
                assignContent =
                    $"工单已分配给{assignedUser.DisplayName}（{GetTicketRoleName(assignedUser.Role)}）";
            }
            else
            {
                /*
                 * 重新分配：
                 *
                 * 需要把旧处理人的名字也查出来。
                 */
                var oldAssignedUser =
                    await _dbContext.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.Id ==
                                oldAssignedToUserId.Value
                        );


                /*
                 * 理论上正常业务中旧用户应该存在。
                 *
                 * 但为了避免历史数据异常导致接口失败，
                 * 这里给一个兜底名称。
                 */
                var oldAssignedName =
                    oldAssignedUser?.DisplayName
                    ?? "原处理人";


                assignContent =
                    $"工单由{oldAssignedName}重新分配给{assignedUser.DisplayName}（{GetTicketRoleName(assignedUser.Role)}）";
            }


            /*
             * 创建一条 TicketRecord。
             *
             * 分配属于系统业务动作，
             * 客户可以知道当前问题由谁处理，
             * 所以 IsInternal = false。
             */
            var assignRecord =
                new TicketRecord
                {
                    TicketId =
                        ticket.Id,

                    CreatedByUserId =
                        currentUserId,

                    RecordType =
                        "Assign",

                    Content =
                        assignContent,

                    IsInternal =
                        false,

                    CreatedAt =
                        now
                };


            _dbContext.TicketRecords.Add(
                assignRecord
            );


            /*
             * ==========================================
             * 10. 一次保存
             * ==========================================
             *
             * 同时保存：
             *
             * Ticket 状态
             * Ticket 处理人
             * TicketRecord 分配记录
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
          * 发布“工单分配 / 重新分配”通知事件
          * ==========================================
          *
          * Controller 不再负责：
          *
          * 通知哪个用户
          * 是否发送钉钉
          * 是否 @
          * SignalR 推送
          *
          * 这里只负责判断：
          *
          * 本次业务动作到底是什么。
          */
            await _notificationEventService.PublishAsync(
                new NotificationEventRequest
                {
                    /*
                     * ==========================================
                     * 第一次分配 / 重新分配
                     * ==========================================
                     */
                    EventKey =
                        isReassignment
                            ? NotificationEventKeys
                                .TicketReassigned
                            : NotificationEventKeys
                                .TicketAssigned,


                    /*
                     * 两种事件的默认策略目前都是：
                     *
                     * RecipientStrategy = Assignee
                     *
                     * Resolver 会重新读取：
                     *
                     * Ticket.AssignedToUserId
                     *
                     * 得到刚刚保存的新处理人。
                     */
                    Context =
                        new NotificationRecipientContext
                        {
                            TicketId =
                                ticket.Id
                        },


                    /*
                     * 标题根据业务动作区分。
                     */
                    Title =
                        isReassignment
                            ? $"工单已重新分配：{ticket.TicketNo}"
                            : $"工单已分配：{ticket.TicketNo}",


                    /*
                     * 当前继续复用已经存在的
                     * 通知正文生成方法。
                     */
                    Content =
                        BuildTicketAssignedNotificationContent(
                            ticket,

                            customer?.Name
                                ?? string.Empty,

                            software?.Name
                                ?? string.Empty,

                            assignedUser,

                            currentUser
                        ),


                    /*
                     * 紧急工单动态提高通知等级。
                     *
                     * 其他情况使用策略表默认 Info。
                     */
                    Level =
                        ticket.Priority == "Urgent"
                            ? "Warning"
                            : null,


                    TargetUrl =
                        $"/tickets?ticketId={ticket.Id}",


                    /*
                     * ==========================================
                     * 去重 Key
                     * ==========================================
                     *
                     * 使用 assignRecord.Id
                     * 比 UpdatedAt.Ticks 更能表达业务含义。
                     *
                     * 每一次真实的分配行为都会生成
                     * 一条新的 Assign TicketRecord。
                     *
                     * 因此：
                     *
                     * ticket:15:assigned:80
                     *
                     * 就明确表示：
                     *
                     * 第 80 条业务记录对应的这次分配。
                     */
                    DedupKey =
                        isReassignment
                            ? $"ticket:{ticket.Id}:" +
                              $"reassigned:{assignRecord.Id}"
                            : $"ticket:{ticket.Id}:" +
                              $"assigned:{assignRecord.Id}",


                    /*
                     * 暂时保持现有前端使用的 Type。
                     *
                     * 第一次分配和重新分配，
                     * 当前前端都可以按 TicketAssigned 显示。
                     */
                    NotificationType =
                        "TicketAssigned"
                }
            );
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

                    recordId =
    assignRecord.Id,

                    ticket.UpdatedAt
                }
            );
        }
        
        /// <summary>
         /// 给工单增加一条处理记录。
         ///
         /// POST /api/tickets/{id}/records
         /// </summary>
        [HttpPost("{id}/records")]
        [Authorize(
            Roles = "Admin,Support,Developer,Customer"
        )]
        public async Task<IActionResult> CreateTicketRecord(
            int id,
            CreateTicketRecordRequest request)
        {
            /*
             * ==========================================
             * 1. 基础参数检查
             * ==========================================
             */

            if (id <= 0)
            {
                return BadRequest(
                    "工单ID无效"
                );
            }


            if (string.IsNullOrWhiteSpace(
                    request.Content))
            {
                return BadRequest(
                    "处理内容不能为空"
                );
            }


            /*
             * 防止单条回复无限长。
             *
             * 当前第一版先限制 5000 字符。
             */
            if (request.Content.Trim().Length > 5000)
            {
                return BadRequest(
                    "处理内容不能超过5000个字符"
                );
            }


            /*
             * ==========================================
             * 2. 获取当前登录用户ID
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
             * 3. 查询当前用户最新状态
             * ==========================================
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
             * 4. 查询工单
             * ==========================================
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
             * 5. 根据角色判断是否有权写记录
             * ==========================================
             */

            switch (currentUser.Role)
            {
                /*
                 * Admin：
                 * 可以操作所有工单。
                 */
                case "Admin":
                    break;


                /*
                 * Support：
                 * 可以操作所有工单。
                 */
                case "Support":
                    break;


                /*
                 * Developer：
                 * 只能处理分配给自己的工单。
                 */
                case "Developer":

                    if (
                        ticket.AssignedToUserId
                        !=
                        currentUser.Id
                    )
                    {
                        return Forbid();
                    }

                    break;


                /*
                 * Customer：
                 * 只能操作自己公司的工单。
                 */
                case "Customer":

                    if (!currentUser.CustomerId.HasValue)
                    {
                        return Unauthorized(
                            "当前用户未绑定客户"
                        );
                    }


                    if (
                        ticket.CustomerId
                        !=
                        currentUser.CustomerId.Value
                    )
                    {
                        return Forbid();
                    }


                    /*
                     * Customer 绝对不能创建内部记录。
                     */
                    if (request.IsInternal)
                    {
                        return BadRequest(
                            "客户用户不能创建内部记录"
                        );
                    }

                    break;


                default:

                    return Forbid();
            }


            /*
             * ==========================================
             * 6. 已关闭工单不允许继续回复
             * ==========================================
             */

            if (ticket.Status == "Closed")
            {
                return BadRequest(
                    "工单已经关闭，不能继续添加处理记录"
                );
            }

            /*
 * 本次处理记录统一使用同一个时间。
 */
            var now =
                DateTime.UtcNow;


            /*
             * ==========================================
             * 记录首次响应时间
             * ==========================================
             *
             * 首次响应必须满足：
             *
             * 1. 不是 Customer 自己回复
             * 2. 不是内部备注
             * 3. FirstResponseAt 还没有记录
             *
             * Admin / Support / Developer
             * 第一次给客户公开回复，
             * 才算真正首次响应。
             */
            var isStaffPublicReply =
                currentUser.Role != "Customer"
                &&
                !request.IsInternal;


            if (
                isStaffPublicReply
                &&
                !ticket.FirstResponseAt.HasValue
            )
            {
                ticket.FirstResponseAt =
                    now;
            }


            /*
             * 有新的处理记录，
             * 工单最后更新时间也应该变化。
             */
            ticket.UpdatedAt =
                now;


            /*
             * ==========================================
             * 7. 创建处理记录
             * ==========================================
             */

            var record =
                new TicketRecord
                {
                    TicketId =
                        ticket.Id,


                    CreatedByUserId =
                        currentUser.Id,


                    /*
                     * 当前这个接口表示普通回复。
                     */
                    RecordType =
                        "Comment",


                    Content =
                        request.Content.Trim(),


                    IsInternal =
                        request.IsInternal,


                    CreatedAt =
                      now
                };


            _dbContext.TicketRecords.Add(
     record
 );

            /*
 * ==========================================
 * 回复通知
 * ==========================================
 *
 * 当前开始逐步迁移到新的通知策略系统。
 *
 * 本轮只迁移：
 *
 * Customer → 公司人员
 *
 * 公司人员 → Customer
 *
 * 暂时继续保留旧通知逻辑，
 * 下一轮再迁移。
 */
            var isCustomerReply =
                currentUser.Role == "Customer";


            /*
             * ==========================================
             * 公司人员公开回复客户
             * ==========================================
             *
             * 这一部分暂时仍使用旧通知逻辑。
             *
             * 注意：
             * 内部备注绝对不能通知客户。
             */
            if (!isCustomerReply &&
                !request.IsInternal)
            {
                var customerUserIds =
                    await _dbContext.Users
                        .AsNoTracking()
                        .Where(x =>
                            x.IsEnabled
                            &&
                            x.Role == "Customer"
                            &&
                            x.CustomerId ==
                                ticket.CustomerId
                        )
                        .Select(x => x.Id)
                        .ToListAsync();


                foreach (var customerUserId
                         in customerUserIds)
                {
                    await _notificationService.AddAsync(
                        userId:
                            customerUserId,

                        type:
                            "TicketReply",

                        title:
                            "您的工单有新的回复",

                        content:
                            $"{currentUser.DisplayName}回复了工单 " +
                            $"{ticket.TicketNo}：{ticket.Title}",

                        level:
                            "Info",

                        targetUrl:
                            $"/tickets?ticketId={ticket.Id}",

                        dedupKey:
                            null
                    );
                }
            }


            await _dbContext.SaveChangesAsync();
            /*
 * ==========================================
 * 客户追加回复通知
 * ==========================================
 *
 * 这里只处理：
 *
 * Customer → 公司
 *
 * 通知给谁已经不由 Controller 判断。
 *
 * NotificationPolicy 中配置：
 *
 * EventKey：
 * Ticket.CustomerReplied
 *
 * RecipientStrategy：
 * AssigneeOrSupport
 *
 *
 * NotificationRecipientResolver 会自动处理：
 *
 * 有 AssignedToUserId
 *     ↓
 * 通知当前处理人
 *
 * 没有 AssignedToUserId
 *     ↓
 * 通知所有 Support
 */
if (isCustomerReply)
{
    await _notificationEventService.PublishAsync(
        new NotificationEventRequest
        {
            /*
             * 业务事件。
             */
            EventKey =
                NotificationEventKeys
                    .TicketCustomerReplied,


            /*
             * Resolver 根据 TicketId
             * 查询当前处理人。
             */
            Context =
                new NotificationRecipientContext
                {
                    TicketId =
                        ticket.Id
                },


            /*
             * 不再区分：
             *
             * “客户回复了工单”
             *
             * 和：
             *
             * “客户回复了未分配工单”
             *
             * 因为“通知谁”已经由策略负责。
             */
            Title =
                $"客户回复了工单：{ticket.TicketNo}",


            /*
             * 当前先保持消息简洁。
             *
             * 完整回复内容仍然可以进入
             * 工单工作台查看。
             */
            Content =
                $"{currentUser.DisplayName}回复了工单 " +
                $"{ticket.TicketNo}：{ticket.Title}",


            /*
             * 不指定 Level。
             *
             * 自动使用：
             *
             * NotificationPolicy.DefaultLevel
             *
             * 当前默认为 Info。
             */


            TargetUrl =
                $"/tickets?ticketId={ticket.Id}",


            /*
             * ==========================================
             * 去重 Key
             * ==========================================
             *
             * 这里使用 record.Id 非常合适。
             *
             * 因为客户每回复一次，
             * 都会产生一条新的 TicketRecord。
             *
             * 例如：
             *
             * ticket:15:customer-reply:81
             * ticket:15:customer-reply:94
             *
             * 每次回复都是独立业务事件，
             * 但同一次回复不会重复通知。
             */
            DedupKey =
                $"ticket:{ticket.Id}:" +
                $"customer-reply:{record.Id}",


            /*
             * 暂时兼容当前前端已有的通知类型。
             */
            NotificationType =
                "TicketReply"
        }
    );
}
            await _notificationService.PushPendingAsync();

            /*
             * ==========================================
             * 9. 返回
             * ==========================================
             */

            return StatusCode(
                StatusCodes.Status201Created,
                new
                {
                    message =
                        "处理记录添加成功",

                    record.Id,

                    record.TicketId,

                    record.RecordType,

                    record.Content,

                    record.IsInternal,

                    record.CreatedByUserId,

                    createdByName =
                        currentUser.DisplayName,

                    createdByRole =
                        currentUser.Role,

                    record.CreatedAt
                }
            );
        }/// <summary>
         /// 查询一个工单的处理记录。
         ///
         /// GET /api/tickets/{id}/records
         /// </summary>
        [HttpGet("{id}/records")]
        [Authorize(
            Roles = "Admin,Support,Developer,Customer"
        )]
        public async Task<IActionResult> GetTicketRecords(
            int id)
        {
            /*
             * ==========================================
             * 1. 获取当前登录用户
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
             * 2. 查询工单
             * ==========================================
             */

            var ticket =
                await _dbContext.Tickets
                    .AsNoTracking()
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
             * 3. 权限检查
             * ==========================================
             */

            switch (currentUser.Role)
            {
                case "Admin":

                case "Support":
                    /*
                     * Admin / Support：
                     * 可以看全部。
                     */
                    break;


                case "Developer":

                    /*
                     * Developer：
                     * 只能看分配给自己的工单。
                     */
                    if (
                        ticket.AssignedToUserId
                        !=
                        currentUser.Id
                    )
                    {
                        return Forbid();
                    }

                    break;


                case "Customer":

                    /*
                     * Customer：
                     * 只能看自己公司的工单。
                     */
                    if (!currentUser.CustomerId.HasValue)
                    {
                        return Unauthorized(
                            "当前用户未绑定客户"
                        );
                    }


                    if (
                        ticket.CustomerId
                        !=
                        currentUser.CustomerId.Value
                    )
                    {
                        return Forbid();
                    }

                    break;


                default:

                    return Forbid();
            }


            /*
             * ==========================================
             * 4. 构造处理记录查询
             * ==========================================
             */

            IQueryable<TicketRecord> query =
                _dbContext.TicketRecords
                    .AsNoTracking()
                    .Where(
                        x => x.TicketId == id
                    );


            /*
             * ==========================================
             * 5. Customer 看不到内部记录
             * ==========================================
             */

            if (currentUser.Role == "Customer")
            {
                query =
                    query.Where(
                        x => !x.IsInternal
                    );
            }


            /*
             * ==========================================
             * 6. 查询时间线
             * ==========================================
             */

            var records =
                await query
                    /*
                     * 时间线按照最早 → 最新排列。
                     */
                    .OrderBy(
                        x => x.CreatedAt
                    )

                    .Select(
                        x => new
                        {
                            x.Id,

                            x.TicketId,

                            x.RecordType,

                            x.Content,

                            x.IsInternal,

                            x.CreatedByUserId,

                            CreatedByName =
                                x.CreatedByUser.DisplayName,

                            CreatedByRole =
                                x.CreatedByUser.Role,

                            x.CreatedAt
                        }
                    )

                    .ToListAsync();


            /*
             * ==========================================
             * 7. 返回
             * ==========================================
             */

            return Ok(
                records
            );
        }/// <summary>
         /// 将工单标记为已解决。
         ///
         /// PUT /api/tickets/{id}/resolve
         ///
         /// 可以执行：
         /// Admin
         /// Support
         /// Developer
         ///
         /// Customer 不能自己把工单标记为已解决。
         /// </summary>
        [HttpPut("{id}/resolve")]
        [Authorize(
            Roles = "Admin,Support,Developer"
        )]
        public async Task<IActionResult> ResolveTicket(
            int id,
            ResolveTicketRequest request)
        {
            /*
             * ==========================================
             * 1. 参数检查
             * ==========================================
             */

            if (id <= 0)
            {
                return BadRequest(
                    "工单ID无效"
                );
            }


            if (string.IsNullOrWhiteSpace(
                    request.Content))
            {
                return BadRequest(
                    "请输入问题解决说明"
                );
            }


            if (request.Content.Trim().Length > 5000)
            {
                return BadRequest(
                    "解决说明不能超过5000个字符"
                );
            }


            /*
             * ==========================================
             * 2. 获取当前登录用户ID
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
             * 3. 查询当前用户最新状态
             * ==========================================
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
             * 4. 查询工单
             * ==========================================
             *
             * 这里不能 AsNoTracking。
             *
             * 因为马上要修改 Ticket：
             *
             * Status
             * ResolvedAt
             * UpdatedAt
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
             * 5. 检查工单当前状态
             * ==========================================
             */

            if (ticket.Status == "Closed")
            {
                return BadRequest(
                    "已关闭的工单不能标记为已解决"
                );
            }


            if (ticket.Status == "Resolved")
            {
                return BadRequest(
                    "当前工单已经是已解决状态"
                );
            }


            /*
             * ==========================================
             * 6. 根据角色检查处理权限
             * ==========================================
             */

            switch (currentUser.Role)
            {
                /*
                 * Admin：
                 * 可以处理任意工单。
                 */
                case "Admin":
                    break;


                /*
                 * Support：
                 * 当前第一版可以处理任意客户工单。
                 */
                case "Support":
                    break;


                /*
                 * Developer：
                 * 只能解决分配给自己的工单。
                 */
                case "Developer":

                    if (
                        ticket.AssignedToUserId
                        !=
                        currentUser.Id
                    )
                    {
                        return Forbid();
                    }

                    break;


                default:

                    return Forbid();
            }


            /*
             * ==========================================
             * 7. 修改工单状态
             * ==========================================
             */

            var now =
                DateTime.UtcNow;
            /*
 * 如果前面从来没有公开回复，
 * 而工作人员直接给出了“解决说明”，
 * 那么这次解决说明就是第一次正式响应。
 */
            if (!ticket.FirstResponseAt.HasValue)
            {
                ticket.FirstResponseAt =
                    now;
            }

            ticket.Status =
                "Resolved";


            ticket.ResolvedAt =
                now;


            ticket.UpdatedAt =
                now;


            /*
             * ==========================================
             * 8. 自动创建一条 Resolve 历史记录
             * ==========================================
             *
             * 这一步非常重要。
             *
             * 如果我们只修改：
             *
             * Status = Resolved
             *
             * 那么以后只能知道：
             *
             * “这个工单解决过”
             *
             * 却不知道：
             *
             * 谁解决的？
             * 什么时间？
             * 怎么解决的？
             *
             * 所以所有关键业务动作，
             * 最好留下历史流水。
             */

            var record =
                new TicketRecord
                {
                    TicketId =
                        ticket.Id,


                    CreatedByUserId =
                        currentUser.Id,


                    RecordType =
                        "Resolve",


                    Content =
                        request.Content.Trim(),


                    /*
                     * Resolve 属于正式解决说明，
                     * 客户可以看到。
                     */
                    IsInternal =
                        false,


                    CreatedAt =
                        now
                };


            _dbContext.TicketRecords.Add(
                record
            );
            await NotifyCustomerUsersAsync(
    ticket,
    "TicketResolved",
    "您的工单已解决",
    $"工单 {ticket.TicketNo} 已由 {currentUser.DisplayName} 标记为已解决。"
);

            /*
             * ==========================================
             * 9. 一次 SaveChanges
             * ==========================================
             *
             * 同时保存：
             *
             * Ticket 状态变化
             *
             * +
             *
             * TicketRecord 解决记录
             */

            await _dbContext.SaveChangesAsync();
            await _notificationService.PushPendingAsync();

            /*
             * ==========================================
             * 10. 返回结果
             * ==========================================
             */

            return Ok(
                new
                {
                    message =
                        "工单已标记为解决",

                    ticket.Id,

                    ticket.TicketNo,

                    ticket.Title,

                    ticket.Status,

                    ticket.AssignedToUserId,

                    ticket.ResolvedAt,

                    ticket.UpdatedAt,

                    resolution =
                        new
                        {
                            record.Id,

                            record.RecordType,

                            record.Content,

                            record.CreatedByUserId,

                            createdByName =
                                currentUser.DisplayName,

                            createdByRole =
                                currentUser.Role,

                            record.CreatedAt
                        }
                }
            );
        }/// <summary>
         /// 关闭工单。
         ///
         /// PUT /api/tickets/{id}/close
         ///
         /// 当前允许：
         /// Customer
         /// Admin
         /// Support
         ///
         /// Customer 只能关闭自己公司的工单。
         /// </summary>
        [HttpPut("{id}/close")]
        [Authorize(
            Roles = "Admin,Support,Customer"
        )]
        public async Task<IActionResult> CloseTicket(
            int id,
            CloseTicketRequest request)
        {
            /*
             * ==========================================
             * 1. 参数检查
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
             * 2. 获取当前登录用户ID
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
             * 3. 查询当前用户
             * ==========================================
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
             * 4. 查询工单
             * ==========================================
             *
             * 这里要修改 Ticket，
             * 所以不要使用 AsNoTracking。
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
             * 5. 状态检查
             * ==========================================
             */

            if (ticket.Status == "Closed")
            {
                return BadRequest(
                    "当前工单已经关闭"
                );
            }


            /*
             * 当前设计：
             *
             * 工单必须先 Resolved，
             * 才允许正式 Close。
             *
             * 防止：
             *
             * Pending
             * ↓
             * 直接 Closed
             */
            if (ticket.Status != "Resolved")
            {
                return BadRequest(
                    "只有已解决的工单才能关闭"
                );
            }


            /*
             * ==========================================
             * 6. 数据权限检查
             * ==========================================
             */

            switch (currentUser.Role)
            {
                case "Admin":

                case "Support":
                    /*
                     * 内部管理员和售后
                     * 可以关闭任意已解决工单。
                     */
                    break;


                case "Customer":

                    /*
                     * Customer 必须绑定客户。
                     */
                    if (!currentUser.CustomerId.HasValue)
                    {
                        return Unauthorized(
                            "当前用户未绑定客户"
                        );
                    }


                    /*
                     * 只能关闭自己公司的工单。
                     */
                    if (
                        ticket.CustomerId
                        !=
                        currentUser.CustomerId.Value
                    )
                    {
                        return Forbid();
                    }

                    break;


                default:

                    return Forbid();
            }


            /*
             * ==========================================
             * 7. 修改 Ticket 状态
             * ==========================================
             */

            var now =
                DateTime.UtcNow;


            ticket.Status =
       "Closed";


            /*
             * 记录工单真正结束的时间。
             */
            ticket.ClosedAt =
                now;


            ticket.UpdatedAt =
                now;


            /*
             * ResolvedAt 不清空。
             *
             * 因为它表示：
             *
             * “什么时候解决的”
             *
             * Close 只是正式结束工单。
             */


            /*
             * ==========================================
             * 8. 创建 Close 历史记录
             * ==========================================
             */

            var content =
                string.IsNullOrWhiteSpace(
                    request.Content
                )
                    ? "工单已关闭"
                    : request.Content.Trim();


            var record =
                new TicketRecord
                {
                    TicketId =
                        ticket.Id,

                    CreatedByUserId =
                        currentUser.Id,

                    RecordType =
                        "Close",

                    Content =
                        content,

                    IsInternal =
                        false,

                    CreatedAt =
                        now
                };


            _dbContext.TicketRecords.Add(
                record
            );
            await NotifyCustomerUsersAsync(
    ticket,
    "TicketClosed",
    "您的工单已关闭",
    $"工单 {ticket.TicketNo} 已关闭。"
);

            /*
             * ==========================================
             * 9. 一次保存
             * ==========================================
             */

            await _dbContext.SaveChangesAsync();


            /*
             * ==========================================
             * 10. 返回结果
             * ==========================================
             */

            return Ok(
                new
                {
                    message =
                        "工单已关闭",

                    ticket.Id,

                    ticket.TicketNo,

                    ticket.Status,

                    ticket.FirstResponseAt,

                    ticket.ResolvedAt,

                    ticket.ClosedAt,

                    ticket.UpdatedAt,

                    closedByUserId =
                        currentUser.Id,

                    closedByName =
                        currentUser.DisplayName,

                    recordId =
                        record.Id
                }
            );
        }/// <summary>
         /// 重新打开工单。
         ///
         /// PUT /api/tickets/{id}/reopen
         ///
         /// 用于：
         ///
         /// 已经 Resolved 或 Closed 的工单
         /// 实际问题仍然存在时重新进入处理流程。
         /// </summary>
        [HttpPut("{id}/reopen")]
        [Authorize(
            Roles = "Admin,Support,Customer"
        )]
        public async Task<IActionResult> ReopenTicket(
            int id,
            ReopenTicketRequest request)
        {
            /*
             * ==========================================
             * 1. 参数检查
             * ==========================================
             */

            if (id <= 0)
            {
                return BadRequest(
                    "工单ID无效"
                );
            }


            if (string.IsNullOrWhiteSpace(
                    request.Content))
            {
                return BadRequest(
                    "请输入重新打开工单的原因"
                );
            }


            if (request.Content.Trim().Length > 5000)
            {
                return BadRequest(
                    "重新打开原因不能超过5000个字符"
                );
            }


            /*
             * ==========================================
             * 2. 获取当前用户ID
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
             * 3. 查询当前用户
             * ==========================================
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
             * 4. 查询工单
             * ==========================================
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
             * 5. 当前状态必须允许 Reopen
             * ==========================================
             *
             * 当前只允许：
             *
             * Resolved
             * Closed
             */

            if (
                ticket.Status != "Resolved"
                &&
                ticket.Status != "Closed"
            )
            {
                return BadRequest(
                    "当前工单状态不允许重新打开"
                );
            }


            /*
             * ==========================================
             * 6. 权限检查
             * ==========================================
             */

            switch (currentUser.Role)
            {
                case "Admin":

                case "Support":
                    break;


                case "Customer":

                    if (!currentUser.CustomerId.HasValue)
                    {
                        return Unauthorized(
                            "当前用户未绑定客户"
                        );
                    }


                    if (
                        ticket.CustomerId
                        !=
                        currentUser.CustomerId.Value
                    )
                    {
                        return Forbid();
                    }

                    break;


                default:

                    return Forbid();
            }


            /*
             * ==========================================
             * 7. 修改工单状态
             * ==========================================
             */

            var now =
                DateTime.UtcNow;


            ticket.Status =
     "Processing";


            /*
             * 问题重新进入处理流程，
             * 原来的“最终解决时间”失效。
             */
            ticket.ResolvedAt =
                null;


            /*
             * 如果原来已经 Closed，
             * 重新打开以后当然就不再是关闭状态，
             * 所以关闭时间也要清空。
             */
            ticket.ClosedAt =
                null;


            /*
             * FirstResponseAt 不清空。
             *
             * 因为它描述的是：
             *
             * 这个工单历史上第一次响应发生在什么时候。
             */
            ticket.UpdatedAt =
                now;


            /*
             * ==========================================
             * 8. 创建 Reopen 历史记录
             * ==========================================
             */

            var record =
                new TicketRecord
                {
                    TicketId =
                        ticket.Id,

                    CreatedByUserId =
                        currentUser.Id,

                    RecordType =
                        "Reopen",

                    Content =
                        request.Content.Trim(),

                    IsInternal =
                        false,

                    CreatedAt =
                        now
                };


            _dbContext.TicketRecords.Add(
                record
            );
            await NotifyCustomerUsersAsync(
    ticket,
    "TicketReopened",
    "您的工单已重新打开",
    $"工单 {ticket.TicketNo} 已重新进入处理中。",
    "Warning"
);

            /*
             * ==========================================
             * 9. 保存
             * ==========================================
             */

            await _dbContext.SaveChangesAsync();


            /*
             * ==========================================
             * 10. 返回
             * ==========================================
             */

            return Ok(
                new
                {
                    message =
                        "工单已重新打开",

                    ticket.Id,

                    ticket.TicketNo,

                    ticket.Status,

                    ticket.AssignedToUserId,

                    ticket.FirstResponseAt,

                    ticket.ResolvedAt,

                    ticket.ClosedAt,

                    ticket.UpdatedAt,

                    reopenedByUserId =
                        currentUser.Id,

                    reopenedByName =
                        currentUser.DisplayName,

                    recordId =
                        record.Id
                }
            );
        }/// <summary>
         /// 获取可以作为工单处理人的用户。
         ///
         /// GET /api/tickets/assignable-users
         ///
         /// 只有 Admin / Support 可以调用。
         ///
         /// 当前允许作为处理人的角色：
         /// Support
         /// Developer
         /// </summary>
        [HttpGet("assignable-users")]
        [Authorize(Roles = "Admin,Support")]
        public async Task<IActionResult> GetAssignableUsers()
        {
            var users =
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
                    .OrderBy(x => x.Role)
                    .ThenBy(x => x.DisplayName)
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

            return Ok(users);
        }

        /// <summary>
        /// 工单模块中使用的角色中文名称。
        /// </summary>
        private static string GetTicketRoleName(
            string role)
        {
            return role switch
            {
                "Admin" =>
                    "管理员",

                "Support" =>
                    "售后",

                "Developer" =>
                    "开发",

                "Customer" =>
                    "客户",

                "Sales" =>
                    "销售",

                _ =>
                    role
            };
        }


        private async Task NotifyCustomerUsersAsync(
    Ticket ticket,
    string type,
    string title,
    string content,
    string level = "Info")
        {
            var userIds = await _dbContext.Users
                .AsNoTracking()
                .Where(x =>
                    x.IsEnabled &&
                    x.Role == "Customer" &&
                    x.CustomerId == ticket.CustomerId)
                .Select(x => x.Id)
                .ToListAsync();

            foreach (var userId in userIds)
            {
                await _notificationService.AddAsync(
                    userId: userId,
                    type: type,
                    title: title,
                    content: content,
                    level: level,
                    targetUrl: $"/tickets?ticketId={ticket.Id}",
                    dedupKey: null
                );
            }
        }

        /// <summary>
        /// 获取工单优先级中文名称。
        ///
        /// 后端数据库仍然保存英文枚举值：
        ///
        /// Unclassified
        /// Low
        /// Normal
        /// High
        /// Urgent
        ///
        /// 这里只负责把它转换成适合用户阅读的文字。
        /// </summary>
        private static string GetTicketPriorityName(
            string? priority)
        {
            return priority switch
            {
                "Unclassified" => "待分诊",
                "Low" => "低",
                "Normal" => "普通",
                "High" => "高",
                "Urgent" => "紧急",

                _ => string.IsNullOrWhiteSpace(priority)
                    ? "未设置"
                    : priority
            };
        }


        /// <summary>
        /// 防止问题描述太长，
        /// 导致钉钉群消息变得非常臃肿。
        ///
        /// 第一版最多显示 200 个字符。
        /// 完整内容仍然在系统工单页面查看。
        /// </summary>
        private static string GetTicketDescriptionSummary(
            string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return "无";
            }

            var text = description.Trim();

            return text.Length <= 200
                ? text
                : text[..200] + "...";
        }


        /// <summary>
        /// 构造“工单创建”外部通知正文。
        ///
        /// 统一在这里控制钉钉、企业微信等
        /// 外部渠道看到的工单信息格式。
        ///
        /// 以后如果增加字段，
        /// 只修改这里即可，
        /// 不要在 Controller 各处重复拼字符串。
        /// </summary>
        private static string BuildTicketCreatedNotificationContent(
            Ticket ticket,
            string customerName,
            string softwareName)
        {
            return
                $"工单号：{ticket.TicketNo}\n" +
                $"客户：{customerName}\n" +
                $"软件：{softwareName}\n" +
                $"标题：{ticket.Title}\n" +
                $"优先级：{GetTicketPriorityName(ticket.Priority)}\n" +
                $"处理人：待分配\n" +
                $"问题描述：{GetTicketDescriptionSummary(ticket.Description)}";
        }

        /// <summary>
        /// 构造“工单已分诊/分配”通知正文。
        ///
        /// 此时工单已经明确了：
        ///
        /// 优先级
        /// 处理人
        /// 状态
        ///
        /// 所以信息应该比“新工单”通知更完整。
        /// </summary>
        private static string BuildTicketAssignedNotificationContent(
            Ticket ticket,
            string customerName,
            string softwareName,
            User assignedUser,
            User operatorUser)
        {
            return
                $"工单号：{ticket.TicketNo}\n" +
                $"客户：{customerName}\n" +
                $"软件：{softwareName}\n" +
                $"标题：{ticket.Title}\n" +
                $"优先级：{GetTicketPriorityName(ticket.Priority)}\n" +
                $"处理人：{assignedUser.DisplayName}（{GetTicketRoleName(assignedUser.Role)}）\n" +
                $"分诊人：{operatorUser.DisplayName}\n" +
                $"状态：处理中\n" +
                $"问题描述：{GetTicketDescriptionSummary(ticket.Description)}";
        }
    }
}