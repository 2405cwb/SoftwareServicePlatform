using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Services;

public class SlaNotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaNotificationBackgroundService> _logger;

    public SlaNotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SlaNotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        /*
         * 服务启动后先检查一次，
         * 后面每5分钟检查一次。
         */
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckSlaAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "检查工单 SLA 通知失败");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(5),
                stoppingToken);
        }
    }

    private async Task CheckSlaAsync(
        CancellationToken cancellationToken)
    {
        /*
         * BackgroundService 是 Singleton，
         * DbContext / NotificationService 是 Scoped。
         *
         * 所以每次执行需要自己创建 Scope。
         */
        using var scope =
            _scopeFactory.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var notificationService =
            scope.ServiceProvider
                .GetRequiredService<INotificationService>();


        var now = DateTime.UtcNow;


        /*
         * 当前启用的 SLA 规则。
         *
         * WarningBeforeMinutes 使用当前配置。
         */
        var slaRules =
            await dbContext.TicketSlaRules
                .AsNoTracking()
                .Where(x => x.IsEnabled)
                .ToDictionaryAsync(
                    x => x.Priority,
                    cancellationToken);


        /*
         * 只检查尚未结束，并且已经应用 SLA 的工单。
         */
        var tickets =
            await dbContext.Tickets
                .AsNoTracking()
                .Where(x =>
                    x.Status != "Resolved"
                    &&
                    x.Status != "Closed"
                    &&
                    x.SlaPriority != null)
                .ToListAsync(cancellationToken);


        /*
         * 未分配工单没有负责人，
         * 这种情况通知所有售后。
         */
        var supportUserIds =
            await dbContext.Users
                .AsNoTracking()
                .Where(x =>
                    x.IsEnabled
                    &&
                    x.Role == "Support")
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);


        foreach (var ticket in tickets)
        {
            if (!slaRules.TryGetValue(
                    ticket.SlaPriority!,
                    out var slaRule))
            {
                continue;
            }


            var recipientIds =
                ticket.AssignedToUserId.HasValue
                    ? new[] { ticket.AssignedToUserId.Value }
                    : supportUserIds.ToArray();


            /*
             * ==========================================
             * 首次响应 SLA
             * ==========================================
             */
            if (
                !ticket.FirstResponseAt.HasValue
                &&
                ticket.SlaFirstResponseTargetMinutes.HasValue)
            {
                var deadline =
                    ticket.CreatedAt.AddMinutes(
                        ticket.SlaFirstResponseTargetMinutes.Value);

                await CheckDeadlineAsync(
                    notificationService,
                    ticket,
                    recipientIds,
                    "first-response",
                    "首次响应",
                    deadline,
                    slaRule.WarningBeforeMinutes,
                    now);
            }


            /*
             * ==========================================
             * 解决 SLA
             * ==========================================
             */
            if (
                !ticket.ResolvedAt.HasValue
                &&
                ticket.SlaResolutionTargetMinutes.HasValue)
            {
                var deadline =
                    ticket.CreatedAt.AddMinutes(
                        ticket.SlaResolutionTargetMinutes.Value);

                await CheckDeadlineAsync(
                    notificationService,
                    ticket,
                    recipientIds,
                    "resolution",
                    "解决",
                    deadline,
                    slaRule.WarningBeforeMinutes,
                    now);
            }
        }


        /*
         * NotificationService 只 Add，
         * 最后统一保存。
         */
        await dbContext.SaveChangesAsync(
            cancellationToken);

        await notificationService.PushPendingAsync(
    cancellationToken);
    }


    private static async Task CheckDeadlineAsync(
        INotificationService notificationService,
        Ticket ticket,
        IEnumerable<int> recipientIds,
        string slaType,
        string slaName,
        DateTime deadline,
        int warningBeforeMinutes,
        DateTime now)
    {
        /*
         * 已经超时。
         */
        if (now >= deadline)
        {
            foreach (var userId in recipientIds)
            {
                await notificationService.AddAsync(
                    userId: userId,
                    type: "SlaOverdue",
                    title: $"工单{slaName}已超时",
                    content:
                        $"工单 {ticket.TicketNo} 已超过{slaName} SLA，请尽快处理。",
                    level: "Danger",
                    targetUrl:
                        $"/tickets?ticketId={ticket.Id}",
                    dedupKey:
                        $"ticket:{ticket.Id}:sla:{slaType}:overdue");
            }

            return;
        }


        /*
         * WarningBeforeMinutes = 0
         * 表示不启用提前预警。
         */
        if (warningBeforeMinutes <= 0)
        {
            return;
        }


        var warningTime =
            deadline.AddMinutes(
                -warningBeforeMinutes);


        /*
         * 已进入预警时间。
         */
        if (now >= warningTime)
        {
            foreach (var userId in recipientIds)
            {
                await notificationService.AddAsync(
                    userId: userId,
                    type: "SlaWarning",
                    title: $"工单{slaName}即将超时",
                    content:
                        $"工单 {ticket.TicketNo} 即将超过{slaName} SLA，请及时处理。",
                    level: "Warning",
                    targetUrl:
                        $"/tickets?ticketId={ticket.Id}",
                    dedupKey:
                        $"ticket:{ticket.Id}:sla:{slaType}:warning");
            }
        }
    }
}