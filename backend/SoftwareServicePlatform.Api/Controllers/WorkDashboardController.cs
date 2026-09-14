using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Controllers;

/// <summary>
/// 角色化工作台。
///
/// 与原 DashboardController 的“管理统计”分开：
/// - DashboardController：Admin / Support 的全局分析
/// - WorkDashboardController：每个角色自己的工作入口、个人待办、客户门户概览
/// </summary>
[ApiController]
[Route("api/work-dashboard")]
[Authorize(Roles = "Admin,Support,Developer,Sales,Customer")]
public class WorkDashboardController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public WorkDashboardController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 顶部用户信息 + 左侧菜单待办角标。
    /// 前端每60秒轻量刷新一次。
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Unauthorized();
        }

        string? customerName = null;
        if (user.CustomerId.HasValue)
        {
            customerName = await _dbContext.Customers
                .AsNoTracking()
                .Where(x => x.Id == user.CustomerId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
        }

        var openTicketCount = await GetMyOpenTicketQuery(user).CountAsync();
        var urgentTicketCount = await GetMyOpenTicketQuery(user)
            .CountAsync(x => x.Priority == "Urgent");

        return Ok(new
        {
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.CustomerId,
            customerName,
            openTicketCount,
            urgentTicketCount
        });
    }

    /// <summary>
    /// 当前角色首页需要的数据。
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return Unauthorized();
        }

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        string? customerName = null;
        if (user.CustomerId.HasValue)
        {
            customerName = await _dbContext.Customers
                .AsNoTracking()
                .Where(x => x.Id == user.CustomerId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
        }

        var myOpenQuery = GetMyOpenTicketQuery(user);
        var myOpenCount = await myOpenQuery.CountAsync();
        var myUrgentCount = await myOpenQuery.CountAsync(x => x.Priority == "Urgent");
        var myResolvedThisMonth = await GetMyTicketScope(user)
            .CountAsync(x =>
                (x.Status == "Resolved" || x.Status == "Closed") &&
                x.ResolvedAt.HasValue &&
                x.ResolvedAt.Value >= monthStart);

        var unassignedCount = 0;
        if (user.Role is "Admin" or "Support")
        {
            unassignedCount = await _dbContext.Tickets
                .AsNoTracking()
                .CountAsync(x =>
                    (x.Status == "Pending" || x.Status == "Processing") &&
                    !x.AssignedToUserId.HasValue);
        }

        var customerSoftwareCount = 0;
        if (user.Role == "Customer" && user.CustomerId.HasValue)
        {
            customerSoftwareCount = await _dbContext.CustomerSoftwares
                .AsNoTracking()
                .CountAsync(x =>
                    x.CustomerId == user.CustomerId.Value &&
                    x.IsEnabled &&
                    x.Software != null &&
                    x.Software.IsEnabled);
        }

        var salesCustomerCount = 0;
        var salesSoftwareCount = 0;
        if (user.Role == "Sales")
        {
            // 当前数据库 SalesOwner 还是字符串，没有 UserId 外键。
            // 这里按 DisplayName 精确匹配，避免为了 Dashboard 强行改数据库。
            var customerIds = await _dbContext.Customers
                .AsNoTracking()
                .Where(x => x.IsEnabled && x.SalesOwner == user.DisplayName)
                .Select(x => x.Id)
                .ToListAsync();

            salesCustomerCount = customerIds.Count;

            if (customerIds.Count > 0)
            {
                salesSoftwareCount = await _dbContext.CustomerSoftwares
                    .AsNoTracking()
                    .CountAsync(x => customerIds.Contains(x.CustomerId) && x.IsEnabled);
            }
        }

        var recentTickets = await GetMyTicketScope(user)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(8)
            .Select(x => new
            {
                x.Id,
                x.TicketNo,
                x.Title,
                x.Status,
                x.Priority,
                x.CustomerId,
                customerName = x.Customer.Name,
                x.SoftwareId,
                softwareName = x.Software.Name,
                x.AssignedToUserId,
                assignedToName = x.AssignedToUser == null ? null : x.AssignedToUser.DisplayName,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();

        var attentionSource = await myOpenQuery
            .OrderByDescending(x => x.Priority == "Urgent")
            .ThenBy(x => x.CreatedAt)
            .Take(30)
            .Select(x => new
            {
                x.Id,
                x.TicketNo,
                x.Title,
                x.Status,
                x.Priority,
                customerName = x.Customer.Name,
                softwareName = x.Software.Name,
                x.CreatedAt,
                x.FirstResponseAt,
                x.SlaFirstResponseTargetMinutes,
                x.SlaResolutionTargetMinutes
            })
            .ToListAsync();

        var warningRules = await _dbContext.TicketSlaRules
            .AsNoTracking()
            .Where(x => x.IsEnabled && x.WarningBeforeMinutes > 0)
            .ToDictionaryAsync(x => x.Priority, x => x.WarningBeforeMinutes);

        var attentionTickets = attentionSource
            .Select(x =>
            {
                DateTime? responseDeadline = x.SlaFirstResponseTargetMinutes.HasValue
                    ? x.CreatedAt.AddMinutes(x.SlaFirstResponseTargetMinutes.Value)
                    : null;
                DateTime? resolutionDeadline = x.SlaResolutionTargetMinutes.HasValue
                    ? x.CreatedAt.AddMinutes(x.SlaResolutionTargetMinutes.Value)
                    : null;

                warningRules.TryGetValue(x.Priority, out var warningBefore);

                var responseOverdue = !x.FirstResponseAt.HasValue && responseDeadline.HasValue && now > responseDeadline.Value;
                var resolutionOverdue = resolutionDeadline.HasValue && now > resolutionDeadline.Value;
                var responseWarning = !responseOverdue && !x.FirstResponseAt.HasValue && responseDeadline.HasValue && warningBefore > 0 && now >= responseDeadline.Value.AddMinutes(-warningBefore);
                var resolutionWarning = !resolutionOverdue && resolutionDeadline.HasValue && warningBefore > 0 && now >= resolutionDeadline.Value.AddMinutes(-warningBefore);

                var level = (responseOverdue || resolutionOverdue)
                    ? "danger"
                    : (responseWarning || resolutionWarning || x.Priority == "Urgent")
                        ? "warning"
                        : "normal";

                return new
                {
                    x.Id,
                    x.TicketNo,
                    x.Title,
                    x.Status,
                    x.Priority,
                    x.customerName,
                    x.softwareName,
                    x.CreatedAt,
                    riskLevel = level,
                    responseOverdue,
                    resolutionOverdue,
                    responseWarning,
                    resolutionWarning
                };
            })
            .Where(x => x.riskLevel != "normal")
            .OrderBy(x => x.riskLevel == "danger" ? 0 : 1)
            .ThenBy(x => x.CreatedAt)
            .Take(8)
            .ToList();

        return Ok(new
        {
            role = user.Role,
            userId = user.Id,
            displayName = user.DisplayName,
            customerName,
            myOpenCount,
            myUrgentCount,
            myResolvedThisMonth,
            unassignedCount,
            customerSoftwareCount,
            salesCustomerCount,
            salesSoftwareCount,
            recentTickets,
            attentionTickets
        });
    }

    private IQueryable<Ticket> GetMyOpenTicketQuery(User user)
    {
        return GetMyTicketScope(user)
            .Where(x => x.Status == "Pending" || x.Status == "Processing");
    }

    private IQueryable<Ticket> GetMyTicketScope(User user)
    {
        var query = _dbContext.Tickets.AsNoTracking();

        return user.Role switch
        {
            "Admin" => query.Where(x => x.AssignedToUserId == user.Id),
            "Support" => query.Where(x => x.AssignedToUserId == user.Id),
            "Developer" => query.Where(x => x.AssignedToUserId == user.Id),
            "Customer" when user.CustomerId.HasValue => query.Where(x => x.CustomerId == user.CustomerId.Value),
            _ => query.Where(x => false)
        };
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var idText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idText, out var userId))
        {
            return null;
        }

        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsEnabled);
    }
}
