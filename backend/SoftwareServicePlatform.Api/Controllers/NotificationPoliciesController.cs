using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Services.NotificationPolicies;

namespace SoftwareServicePlatform.Api.Controllers;

/// <summary>
/// 通知策略管理接口。
///
/// 仅管理员可访问。
///
/// 这里管理的是：
/// “某类业务事件应该如何通知”
/// 而不是已经产生的 Notification 记录。
/// </summary>
[ApiController]
[Route("api/notification-policies")]
[Authorize(Roles = "Admin")]
public class NotificationPoliciesController
    : ControllerBase
{
    private readonly AppDbContext
        _dbContext;


    public NotificationPoliciesController(
        AppDbContext dbContext)
    {
        _dbContext =
            dbContext;
    }


    /// <summary>
    /// 获取全部通知策略。
    ///
    /// GET /api/notification-policies
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPolicies()
    {
        var policies =
            await _dbContext
                .NotificationPolicies
                .AsNoTracking()
                .Include(x => x.Channels)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.EventKey,
                    x.EventName,
                    x.Description,
                    x.IsEnabled,
                    x.InAppEnabled,
                    x.RecipientStrategy,
                    x.DefaultLevel,
                    x.CreatedAt,
                    x.UpdatedAt,

                    Channels =
                        x.Channels
                            .OrderBy(channel =>
                                channel.Channel
                            )
                            .Select(channel =>
                                new
                                {
                                    channel.Id,
                                    channel.Channel,
                                    channel.IsEnabled,
                                    channel.MentionRecipient,
                                    channel.MentionAll,
                                    channel.CreatedAt,
                                    channel.UpdatedAt
                                }
                            )
                            .ToList()
                })
                .ToListAsync();


        return Ok(
            policies
        );
    }


    /// <summary>
    /// 修改某一业务事件的通知策略。
    ///
    /// PUT /api/notification-policies/{eventKey}
    ///
    /// EventKey 本身不可修改。
    /// </summary>
    [HttpPut("{eventKey}")]
    public async Task<IActionResult> UpdatePolicy(
        string eventKey,
        UpdateNotificationPolicyRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                eventKey))
        {
            return BadRequest(
                "通知事件编码不能为空"
            );
        }


        var policy =
            await _dbContext
                .NotificationPolicies
                .Include(x => x.Channels)
                .FirstOrDefaultAsync(
                    x =>
                        x.EventKey == eventKey
                );


        if (policy == null)
        {
            return NotFound(
                "通知策略不存在"
            );
        }


        /*
         * ==========================================
         * 接收人策略白名单
         * ==========================================
         */
        var allowedRecipientStrategies =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            )
            {
                RecipientStrategies.Support,
                RecipientStrategies.Assignee,
                RecipientStrategies.Customer,
                RecipientStrategies.AssigneeOrSupport,
                RecipientStrategies.AssigneeAndSupport,
                RecipientStrategies.AssigneeSupportAdmin,
                RecipientStrategies.CustomerAndAssignee,
                RecipientStrategies.VersionAudience
            };


        if (!allowedRecipientStrategies.Contains(
                request.RecipientStrategy))
        {
            return BadRequest(
                "通知接收人策略无效"
            );
        }


        /*
         * ==========================================
         * 通知级别白名单
         * ==========================================
         */
        var allowedLevels =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            )
            {
                "Info",
                "Warning",
                "Danger"
            };


        if (!allowedLevels.Contains(
                request.DefaultLevel))
        {
            return BadRequest(
                "默认通知级别无效"
            );
        }


        var duplicateChannel =
            request.Channels
                .GroupBy(
                    x =>
                        x.Channel,
                    StringComparer.OrdinalIgnoreCase
                )
                .FirstOrDefault(
                    x =>
                        x.Count() > 1
                );


        if (duplicateChannel != null)
        {
            return BadRequest(
                $"通知渠道重复：{duplicateChannel.Key}"
            );
        }


        /*
         * 第一版只允许修改数据库中已经存在的渠道配置。
         * 防止管理页面凭空创建尚未实现的渠道。
         */
        foreach (var requestChannel
                 in request.Channels)
        {
            var existingChannel =
                policy.Channels
                    .FirstOrDefault(
                        x =>
                            string.Equals(
                                x.Channel,
                                requestChannel.Channel,
                                StringComparison.OrdinalIgnoreCase
                            )
                    );


            if (existingChannel == null)
            {
                return BadRequest(
                    $"通知渠道尚未配置：{requestChannel.Channel}"
                );
            }
        }


        var now =
            DateTime.UtcNow;


        policy.IsEnabled =
            request.IsEnabled;

        policy.InAppEnabled =
            request.InAppEnabled;

        policy.RecipientStrategy =
            request.RecipientStrategy.Trim();

        policy.DefaultLevel =
            request.DefaultLevel.Trim();

        policy.UpdatedAt =
            now;


        foreach (var channel
                 in policy.Channels)
        {
            var requestChannel =
                request.Channels
                    .FirstOrDefault(
                        x =>
                            string.Equals(
                                x.Channel,
                                channel.Channel,
                                StringComparison.OrdinalIgnoreCase
                            )
                    );


            if (requestChannel == null)
            {
                continue;
            }


            channel.IsEnabled =
                requestChannel.IsEnabled;

            channel.MentionRecipient =
                requestChannel.MentionRecipient;

            channel.MentionAll =
                requestChannel.MentionAll;

            channel.UpdatedAt =
                now;
        }


        await _dbContext.SaveChangesAsync();


        return Ok(
            new
            {
                message =
                    "通知策略保存成功",

                policy.EventKey,
                policy.UpdatedAt
            }
        );
    }
}


/// <summary>
/// 修改通知策略请求。
/// </summary>
public class UpdateNotificationPolicyRequest
{
    public bool IsEnabled
    {
        get;
        set;
    }


    public bool InAppEnabled
    {
        get;
        set;
    }


    public string RecipientStrategy
    {
        get;
        set;
    } = string.Empty;


    public string DefaultLevel
    {
        get;
        set;
    } = "Info";


    public List<
        UpdateNotificationPolicyChannelRequest>
        Channels
    {
        get;
        set;
    } = new();
}


/// <summary>
/// 修改某一个外部通知渠道。
/// </summary>
public class UpdateNotificationPolicyChannelRequest
{
    public string Channel
    {
        get;
        set;
    } = string.Empty;


    public bool IsEnabled
    {
        get;
        set;
    }


    public bool MentionRecipient
    {
        get;
        set;
    }


    public bool MentionAll
    {
        get;
        set;
    }
}
