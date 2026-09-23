using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 管理员运维中心：
    /// API 异常、登录失败、客户端更新失败、操作审计。
    /// </summary>
    [ApiController]
    [Route("api/operations")]
    [Authorize(Roles = "Admin")]
    public class OperationsController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public OperationsController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard(
            int take = 50,
            CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 200);

            var since = DateTime.UtcNow.AddHours(-24);

            var errorCount24h =
                await _dbContext.SystemEventLogs
                    .AsNoTracking()
                    .CountAsync(
                        x => x.CreatedAt >= since
                             && x.Level == "Error",
                        cancellationToken);

            var loginFailureCount24h =
                await _dbContext.SystemEventLogs
                    .AsNoTracking()
                    .CountAsync(
                        x => x.CreatedAt >= since
                             && x.Source == "Auth.LoginFailed",
                        cancellationToken);

            var failedUpdateCount24h =
                await _dbContext.DownloadRecords
                    .AsNoTracking()
                    .CountAsync(
                        x => x.DownloadedAt >= since
                             && x.Status == "Failed",
                        cancellationToken);

            var auditCount24h =
                await _dbContext.AuditLogs
                    .AsNoTracking()
                    .CountAsync(
                        x => x.CreatedAt >= since,
                        cancellationToken);

            var recentErrors =
                await _dbContext.SystemEventLogs
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(take)
                    .Select(x => new
                    {
                        x.Id,
                        x.Level,
                        x.Source,
                        x.Message,
                        x.Detail,
                        x.Path,
                        x.HttpMethod,
                        x.StatusCode,
                        x.UserName,
                        x.IpAddress,
                        x.TraceId,
                        x.CreatedAt
                    })
                    .ToListAsync(cancellationToken);

            var recentAudits =
                await _dbContext.AuditLogs
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(take)
                    .Select(x => new
                    {
                        x.Id,
                        x.UserId,
                        x.UserName,
                        x.DisplayName,
                        x.Role,
                        x.HttpMethod,
                        x.Path,
                        x.ActionName,
                        x.StatusCode,
                        x.DurationMs,
                        x.IpAddress,
                        x.TraceId,
                        x.CreatedAt
                    })
                    .ToListAsync(cancellationToken);

            var recentFailedUpdates =
                await _dbContext.DownloadRecords
                    .AsNoTracking()
                    .Where(x => x.Status == "Failed")
                    .OrderByDescending(x => x.DownloadedAt)
                    .Take(take)
                    .Select(x => new
                    {
                        x.Id,
                        x.CustomerName,
                        x.SoftwareName,
                        x.FromVersion,
                        x.ToVersion,
                        x.DownloadType,
                        x.FileCount,
                        x.FileSize,
                        x.ErrorMessage,
                        x.DownloadedAt
                    })
                    .ToListAsync(cancellationToken);

            return Ok(new
            {
                generatedAt = DateTime.UtcNow,
                summary = new
                {
                    errorCount24h,
                    loginFailureCount24h,
                    failedUpdateCount24h,
                    auditCount24h
                },
                recentErrors,
                recentAudits,
                recentFailedUpdates
            });
        }
    }
}
