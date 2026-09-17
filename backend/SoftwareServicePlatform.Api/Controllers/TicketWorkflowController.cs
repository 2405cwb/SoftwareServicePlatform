using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services;
using System.Security.Claims;

namespace SoftwareServicePlatform.Api.Controllers;

/// <summary>
/// 工单受理 / 分诊工作流。
///
/// 这个 Controller 专门放“售后业务动作”，避免继续把 TicketsController 堆得越来越大。
/// 不新增数据库表，完全复用现有 Ticket / TicketRecord / TicketSlaRule。
/// </summary>
[ApiController]
[Route("api/ticket-workflow")]
[Authorize]
public class TicketWorkflowController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private static readonly string[] Priorities =
    {
        "Low", "Normal", "High", "Urgent"
    };

    private static readonly string[] StaffSources =
    {
        "WeChat", "Phone", "Email", "OnSite", "Internal"
    };

    public TicketWorkflowController(AppDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    /// <summary>
    /// 售后代录工单时需要的客户、软件、处理人选项。
    /// </summary>
    [HttpGet("intake-options")]
    [Authorize(Roles = "Admin,Support")]
    public async Task<IActionResult> GetIntakeOptions()
    {
        var customers = await _dbContext.Customers
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Code,
                softwares = x.CustomerSoftwares
                    .Where(binding =>
                        binding.IsEnabled &&
                        binding.Software != null &&
                        binding.Software.IsEnabled)
                    .OrderBy(binding => binding.Software!.Name)
                    .Select(binding => new
                    {
                        id = binding.SoftwareId,
                        name = binding.Software!.Name,
                        code = binding.Software.Code
                    })
            })
            .ToListAsync();

        var assignees = await _dbContext.Users
            .AsNoTracking()
            .Where(x =>
                x.IsEnabled &&
                (x.Role == "Support" || x.Role == "Developer"))
            .OrderBy(x => x.Role)
            .ThenBy(x => x.DisplayName)
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.Username,
                x.Role
            })
            .ToListAsync();

        return Ok(new
        {
            customers,
            assignees,
            priorities = Priorities,
            sources = StaffSources
        });
    }

    /// <summary>
    /// Admin / Support 代客户录入工单。
    /// 常见场景：微信、电话、现场反馈。
    /// </summary>
    [HttpPost("create")]
    [Authorize(Roles = "Admin,Support")]
    public async Task<IActionResult> CreateByStaff(CreateStaffTicketRequest request)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized("当前用户不存在或已停用");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("请输入工单标题");
        }

        if (request.Title.Trim().Length > 200)
        {
            return BadRequest("工单标题不能超过200个字符");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("请输入问题描述");
        }

        var priority = NormalizePriority(request.Priority);
        if (priority == null)
        {
            return BadRequest("工单优先级无效");
        }

        var source = StaffSources.FirstOrDefault(x =>
            string.Equals(x, request.Source?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (source == null)
        {
            return BadRequest("工单来源无效");
        }

        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CustomerId && x.IsEnabled);

        if (customer == null)
        {
            return BadRequest("客户不存在或已停用");
        }

        var software = await _dbContext.CustomerSoftwares
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == request.CustomerId &&
                x.SoftwareId == request.SoftwareId &&
                x.IsEnabled &&
                x.Software != null &&
                x.Software.IsEnabled)
            .Select(x => x.Software!)
            .FirstOrDefaultAsync();

        if (software == null)
        {
            return BadRequest("该客户未绑定所选软件");
        }

        User? assignee = null;
        if (request.AssignedToUserId.HasValue)
        {
            assignee = await FindValidAssigneeAsync(request.AssignedToUserId.Value);
            if (assignee == null)
            {
                return BadRequest("处理人不存在、已停用或角色不允许");
            }
        }

        var now = DateTime.UtcNow;
        var slaRule = await _dbContext.TicketSlaRules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Priority == priority && x.IsEnabled);

        var ticket = new Ticket
        {
            TicketNo = $"TMP-{Guid.NewGuid():N}",
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = assignee == null ? "Pending" : "Processing",
            Priority = priority,
            Source = source,
            SlaPriority = slaRule == null ? null : priority,
            SlaFirstResponseTargetMinutes = slaRule?.FirstResponseTargetMinutes,
            SlaResolutionTargetMinutes = slaRule?.ResolutionTargetMinutes,
            SlaAppliedAt = slaRule == null ? null : now,
            CustomerId = customer.Id,
            SoftwareId = software.Id,
            CreatedByUserId = currentUser.Id,
            AssignedToUserId = assignee?.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        ticket.TicketNo = $"TK{now:yyyyMMdd}{ticket.Id:D6}";

        _dbContext.TicketRecords.Add(new TicketRecord
        {
            TicketId = ticket.Id,
            CreatedByUserId = currentUser.Id,
            RecordType = "System",
            Content = $"{currentUser.DisplayName}代客户录入工单，来源：{GetSourceName(source)}，优先级：{GetPriorityName(priority)}",
            IsInternal = true,
            CreatedAt = now
        });

        if (assignee != null)
        {
            _dbContext.TicketRecords.Add(new TicketRecord
            {
                TicketId = ticket.Id,
                CreatedByUserId = currentUser.Id,
                RecordType = "Assign",
                Content = $"工单已分配给{assignee.DisplayName}（{GetRoleName(assignee.Role)}）",
                IsInternal = false,
                CreatedAt = now
            });

            await _notificationService.AddAsync(
                    userId: assignee.Id,
                    type: "TicketAssigned",
                    title: "有新的工单分配给你",
                    content:
                        $"工单 {ticket.TicketNo} 已分配给你：{ticket.Title}",
                    level:
                        priority == "Urgent"
                            ? "Danger"
                            : priority == "High"
                                ? "Warning"
                                : "Info",
                    targetUrl:
                        $"/tickets?ticketId={ticket.Id}",
                    dedupKey:
                    null
            );
        }

        await _dbContext.SaveChangesAsync();
        await _notificationService.PushPendingAsync();
        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "工单创建成功",
            ticket.Id,
            ticket.TicketNo,
            ticket.Title,
            ticket.Status,
            ticket.Priority,
            ticket.Source,
            customerName = customer.Name,
            softwareName = software.Name,
            ticket.AssignedToUserId,
            assignedToName = assignee?.DisplayName,
            ticket.CreatedAt
        });
    }

    /// <summary>
    /// 售后分诊：确定优先级，可同时分配处理人。
    ///
    /// 客户 Portal 工单创建时 Priority 会被保护层强制为 Unclassified，
    /// SLA 也为空；只有执行本接口之后才正式获得 SLA 快照。
    /// </summary>
    [HttpPut("{id:int}/triage")]
    [Authorize(Roles = "Admin,Support")]
    public async Task<IActionResult> Triage(int id, TriageTicketRequest request)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized("当前用户不存在或已停用");
        }

        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(x => x.Id == id);
        if (ticket == null)
        {
            return NotFound("工单不存在");
        }

        if (ticket.Status == "Closed")
        {
            return BadRequest("已关闭工单不能重新分诊");
        }

        var priority = NormalizePriority(request.Priority);
        if (priority == null)
        {
            return BadRequest("工单优先级无效");
        }

        User? assignee = null;
        if (request.AssignedToUserId.HasValue)
        {
            assignee = await FindValidAssigneeAsync(request.AssignedToUserId.Value);
            if (assignee == null)
            {
                return BadRequest("处理人不存在、已停用或角色不允许");
            }
        }

        var now = DateTime.UtcNow;
        var oldPriority = ticket.Priority;
        var oldAssigneeId = ticket.AssignedToUserId;

        var slaRule = await _dbContext.TicketSlaRules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Priority == priority && x.IsEnabled);

        ticket.Priority = priority;
        ticket.SlaPriority = slaRule == null ? null : priority;
        ticket.SlaFirstResponseTargetMinutes = slaRule?.FirstResponseTargetMinutes;
        ticket.SlaResolutionTargetMinutes = slaRule?.ResolutionTargetMinutes;
        ticket.SlaAppliedAt = slaRule == null ? null : now;
        ticket.UpdatedAt = now;

        if (assignee != null)
        {
            ticket.AssignedToUserId = assignee.Id;
            if (ticket.Status == "Pending")
            {
                ticket.Status = "Processing";
            }
        }

        _dbContext.TicketRecords.Add(new TicketRecord
        {
            TicketId = ticket.Id,
            CreatedByUserId = currentUser.Id,
            RecordType = "System",
            Content = oldPriority == "Unclassified"
                ? $"售后完成分诊，优先级确定为{GetPriorityName(priority)}"
                : $"工单优先级由{GetPriorityName(oldPriority)}调整为{GetPriorityName(priority)}",
            IsInternal = true,
            CreatedAt = now
        });

        if (assignee != null && oldAssigneeId != assignee.Id)
        {
            _dbContext.TicketRecords.Add(new TicketRecord
            {
                TicketId = ticket.Id,
                CreatedByUserId = currentUser.Id,
                RecordType = "Assign",
                Content = $"工单已分配给{assignee.DisplayName}（{GetRoleName(assignee.Role)}）",
                IsInternal = false,
                CreatedAt = now
            });

            await _notificationService.AddAsync(
                    userId: assignee.Id,
                    type: "TicketAssigned",
                    title: "有新的工单分配给你",
                    content:
                        $"工单 {ticket.TicketNo} 已分配给你：{ticket.Title}",
                    level:
                        ticket.Priority == "Urgent"
                            ? "Danger"
                            : ticket.Priority == "High"
                                ? "Warning"
                                : "Info",
                    targetUrl:
                        $"/tickets?ticketId={ticket.Id}",
                    dedupKey:
                             null
);
        }

        await _dbContext.SaveChangesAsync();
        await _notificationService.PushPendingAsync();
        return Ok(new
        {
            message = "工单分诊完成",
            ticket.Id,
            ticket.Status,
            ticket.Priority,
            ticket.AssignedToUserId,
            assignedToName = assignee?.DisplayName,
            ticket.SlaPriority,
            ticket.SlaFirstResponseTargetMinutes,
            ticket.SlaResolutionTargetMinutes,
            ticket.SlaAppliedAt,
            ticket.UpdatedAt
        });
    }

    /// <summary>
    /// 返回选中工单的 SLA / 分诊元数据，供专业工单工作台右侧信息栏使用。
    /// </summary>
    [HttpGet("{id:int}/meta")]
    [Authorize(Roles = "Admin,Support,Developer")]
    public async Task<IActionResult> GetMeta(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        var ticket = await _dbContext.Tickets
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                Ticket = x,
                CustomerName = x.Customer.Name,
                SoftwareName = x.Software.Name,
                AssignedToName = x.AssignedToUser == null ? null : x.AssignedToUser.DisplayName
            })
            .FirstOrDefaultAsync();

        if (ticket == null)
        {
            return NotFound("工单不存在");
        }

        if (!CanAccessTicket(currentUser, ticket.Ticket))
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        var t = ticket.Ticket;
        DateTime? responseDeadline = t.SlaFirstResponseTargetMinutes.HasValue
            ? t.CreatedAt.AddMinutes(t.SlaFirstResponseTargetMinutes.Value)
            : null;
        DateTime? resolutionDeadline = t.SlaResolutionTargetMinutes.HasValue
            ? t.CreatedAt.AddMinutes(t.SlaResolutionTargetMinutes.Value)
            : null;

        var responseOverdue = !t.FirstResponseAt.HasValue && responseDeadline.HasValue && now > responseDeadline;
        var resolutionOverdue = !t.ResolvedAt.HasValue && !t.ClosedAt.HasValue && resolutionDeadline.HasValue && now > resolutionDeadline;

        return Ok(new
        {
            t.Id,
            t.TicketNo,
            t.Priority,
            t.Source,
            t.Status,
            t.SlaPriority,
            t.SlaFirstResponseTargetMinutes,
            t.SlaResolutionTargetMinutes,
            t.SlaAppliedAt,
            responseDeadline,
            resolutionDeadline,
            responseOverdue,
            resolutionOverdue,
            t.FirstResponseAt,
            t.ResolvedAt,
            t.ClosedAt,
            customerName = ticket.CustomerName,
            softwareName = ticket.SoftwareName,
            assignedToName = ticket.AssignedToName,
            canTriage = currentUser.Role is "Admin" or "Support"
        });
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

    private async Task<User?> FindValidAssigneeAsync(int userId)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == userId &&
                x.IsEnabled &&
                (x.Role == "Support" || x.Role == "Developer"));
    }

    private static bool CanAccessTicket(User user, Ticket ticket)
    {
        return user.Role switch
        {
            "Admin" => true,
            "Support" => true,
            "Developer" => ticket.AssignedToUserId == user.Id,
            "Customer" => user.CustomerId.HasValue && ticket.CustomerId == user.CustomerId.Value,
            _ => false
        };
    }

    private static string? NormalizePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return null;
        }

        return Priorities.FirstOrDefault(x =>
            string.Equals(x, priority.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string GetPriorityName(string priority) => priority switch
    {
        "Low" => "低",
        "Normal" => "普通",
        "High" => "高",
        "Urgent" => "紧急",
        "Unclassified" => "待分诊",
        _ => priority
    };

    private static string GetRoleName(string role) => role switch
    {
        "Support" => "售后",
        "Developer" => "开发",
        "Admin" => "管理员",
        _ => role
    };

    private static string GetSourceName(string source) => source switch
    {
        "WeChat" => "微信",
        "Phone" => "电话",
        "Email" => "邮件",
        "OnSite" => "现场",
        "Internal" => "内部录入",
        _ => source
    };
}

public class CreateStaffTicketRequest
{
    public int CustomerId { get; set; }
    public int SoftwareId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string Source { get; set; } = "WeChat";
    public int? AssignedToUserId { get; set; }
}

public class TriageTicketRequest
{
    public string Priority { get; set; } = "Normal";
    public int? AssignedToUserId { get; set; }
}
