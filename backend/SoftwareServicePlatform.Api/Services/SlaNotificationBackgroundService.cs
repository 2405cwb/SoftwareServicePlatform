using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services.NotificationPolicies;

namespace SoftwareServicePlatform.Api.Services;

/// <summary>
/// 工单 SLA 后台提醒服务。
///
/// 每 5 分钟检查：
/// 首次响应 SLA 和解决 SLA。
///
/// 这里仅判断业务事件是否发生，
/// 通知对象和渠道由 NotificationPolicy 决定。
/// </summary>
public class SlaNotificationBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        SlaNotificationBackgroundService>
        _logger;


    public SlaNotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SlaNotificationBackgroundService> logger)
    {
        _scopeFactory =
            scopeFactory;

        _logger =
            logger;
    }


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckSlaAsync(
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "检查工单 SLA 通知失败"
                );
            }


            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(5),
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }


    private async Task CheckSlaAsync(
        CancellationToken cancellationToken)
    {
        using var scope =
            _scopeFactory.CreateScope();


        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();


        var notificationEventService =
            scope.ServiceProvider
                .GetRequiredService<
                    INotificationEventService>();


        var now =
            DateTime.UtcNow;


        var slaRules =
            await dbContext.TicketSlaRules
                .AsNoTracking()
                .Where(x =>
                    x.IsEnabled
                )
                .ToDictionaryAsync(
                    x =>
                        x.Priority,
                    cancellationToken
                );


        var tickets =
            await dbContext.Tickets
                .AsNoTracking()
                .Where(x =>
                    x.Status != "Resolved"
                    &&
                    x.Status != "Closed"
                    &&
                    x.SlaPriority != null
                )
                .ToListAsync(
                    cancellationToken
                );


        foreach (var ticket in tickets)
        {
            if (!slaRules.TryGetValue(
                    ticket.SlaPriority!,
                    out var slaRule))
            {
                continue;
            }


            if (
                !ticket.FirstResponseAt.HasValue
                &&
                ticket
                    .SlaFirstResponseTargetMinutes
                    .HasValue
            )
            {
                var deadline =
                    ticket.CreatedAt.AddMinutes(
                        ticket
                            .SlaFirstResponseTargetMinutes
                            .Value
                    );


                await CheckDeadlineAsync(
                    dbContext,
                    notificationEventService,
                    ticket,
                    "first-response",
                    "首次响应",
                    deadline,
                    slaRule.WarningBeforeMinutes,
                    now,
                    cancellationToken
                );
            }


            if (
                !ticket.ResolvedAt.HasValue
                &&
                ticket
                    .SlaResolutionTargetMinutes
                    .HasValue
            )
            {
                var deadline =
                    ticket.CreatedAt.AddMinutes(
                        ticket
                            .SlaResolutionTargetMinutes
                            .Value
                    );


                await CheckDeadlineAsync(
                    dbContext,
                    notificationEventService,
                    ticket,
                    "resolution",
                    "解决",
                    deadline,
                    slaRule.WarningBeforeMinutes,
                    now,
                    cancellationToken
                );
            }
        }
    }


    private static async Task CheckDeadlineAsync(
        AppDbContext dbContext,
        INotificationEventService notificationEventService,
        Ticket ticket,
        string slaType,
        string slaName,
        DateTime deadline,
        int warningBeforeMinutes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (now >= deadline)
        {
            var dedupKey =
                $"ticket:{ticket.Id}:" +
                $"sla:{slaType}:overdue";


            /*
             * BackgroundService 每 5 分钟执行。
             *
             * 先通过已有 Notification.DedupKey
             * 判断这一 SLA 事件是否已经处理过，
             * 避免钉钉也每 5 分钟重复发送。
             */
            var alreadySent =
                await dbContext.Notifications
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.DedupKey == dedupKey,
                        cancellationToken
                    );


            if (alreadySent)
            {
                return;
            }


            await notificationEventService.PublishAsync(
                new NotificationEventRequest
                {
                    EventKey =
                        NotificationEventKeys
                            .TicketSlaOverdue,

                    Context =
                        new NotificationRecipientContext
                        {
                            TicketId =
                                ticket.Id
                        },

                    Title =
                        $"工单{slaName}已超时",

                    Content =
                        $"工单 {ticket.TicketNo} " +
                        $"已超过{slaName} SLA，请尽快处理。",

                    Level =
                        "Danger",

                    TargetUrl =
                        $"/tickets?ticketId={ticket.Id}",

                    DedupKey =
                        dedupKey,

                    NotificationType =
                        "SlaOverdue"
                },
                cancellationToken
            );


            return;
        }


        if (warningBeforeMinutes <= 0)
        {
            return;
        }


        var warningTime =
            deadline.AddMinutes(
                -warningBeforeMinutes
            );


        if (now >= warningTime)
        {
            var dedupKey =
                $"ticket:{ticket.Id}:" +
                $"sla:{slaType}:warning";


            var alreadySent =
                await dbContext.Notifications
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.DedupKey == dedupKey,
                        cancellationToken
                    );


            if (alreadySent)
            {
                return;
            }


            await notificationEventService.PublishAsync(
                new NotificationEventRequest
                {
                    EventKey =
                        NotificationEventKeys
                            .TicketSlaWarning,

                    Context =
                        new NotificationRecipientContext
                        {
                            TicketId =
                                ticket.Id
                        },

                    Title =
                        $"工单{slaName}即将超时",

                    Content =
                        $"工单 {ticket.TicketNo} " +
                        $"即将超过{slaName} SLA，请及时处理。",

                    Level =
                        "Warning",

                    TargetUrl =
                        $"/tickets?ticketId={ticket.Id}",

                    DedupKey =
                        dedupKey,

                    NotificationType =
                        "SlaWarning"
                },
                cancellationToken
            );
        }
    }
}
