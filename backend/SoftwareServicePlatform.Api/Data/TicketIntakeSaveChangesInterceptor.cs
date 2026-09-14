using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Data;

/// <summary>
/// 保护客户门户工单入口。
///
/// 客户通过原有 POST /api/tickets 创建工单时，旧接口仍然会接收 Priority。
/// 为了避免客户通过手工 HTTP 请求伪造 Urgent / High，本拦截器在 EF 保存前
/// 对 Portal 新工单做最终兜底：强制进入“待分诊”，并且不提前套用 SLA。
///
/// 售后 / 管理员代录工单使用 TicketWorkflowController，来源不是 Portal，
/// 因此不会被这里覆盖。
/// </summary>
public sealed class TicketIntakeSaveChangesInterceptor : SaveChangesInterceptor
{
    private static void NormalizeCustomerPortalTickets(DbContext? context)
    {
        if (context == null)
        {
            return;
        }

        var entries = context.ChangeTracker
            .Entries<Ticket>()
            .Where(x => x.State == EntityState.Added);

        foreach (var entry in entries)
        {
            var ticket = entry.Entity;

            if (!string.Equals(ticket.Source, "Portal", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ticket.Priority = "Unclassified";
            ticket.SlaPriority = null;
            ticket.SlaFirstResponseTargetMinutes = null;
            ticket.SlaResolutionTargetMinutes = null;
            ticket.SlaAppliedAt = null;
        }
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        NormalizeCustomerPortalTickets(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        NormalizeCustomerPortalTickets(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
