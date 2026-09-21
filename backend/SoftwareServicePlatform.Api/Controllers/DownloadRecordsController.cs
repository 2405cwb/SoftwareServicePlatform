using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Controllers;

/// <summary>
/// 软件安装包下载记录管理。
/// </summary>
[ApiController]
[Route("api/download-records")]
[Authorize(Roles = "Admin,Support")]
public class DownloadRecordsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public DownloadRecordsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 分页查询下载记录。
    ///
    /// Dashboard 跳转支持：keyword / customerId / softwareId / period=thisMonth。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDownloadRecords(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] int? customerId = null,
        [FromQuery] int? softwareId = null,
        [FromQuery] string? period = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.DownloadRecords
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(x =>
                     EF.Functions.ILike(x.UserName, pattern) ||
                     EF.Functions.ILike(x.UserDisplayName, pattern) ||
                     EF.Functions.ILike(x.CustomerName, pattern) ||
                     EF.Functions.ILike(x.SoftwareName, pattern) ||
                     EF.Functions.ILike(x.Version, pattern) ||
                     EF.Functions.ILike(x.FileName, pattern) ||
                     EF.Functions.ILike(x.DownloadType, pattern) ||
                     EF.Functions.ILike(x.FromVersion, pattern) ||
                     EF.Functions.ILike(x.ToVersion, pattern) ||
                     EF.Functions.ILike(x.Status, pattern));
        }

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        if (softwareId.HasValue)
        {
            query = query.Where(x => x.SoftwareId == softwareId.Value);
        }

        if (string.Equals(period, "thisMonth", StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            query = query.Where(x => x.DownloadedAt >= monthStart);
        }

        var total = await query.CountAsync();
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        var items = await query
            .OrderByDescending(x => x.DownloadedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,

                x.UserId,
                x.UserName,
                x.UserDisplayName,

                x.CustomerId,
                x.CustomerName,

                x.SoftwareId,
                x.SoftwareName,

                x.SoftwareVersionId,
                x.Version,

                x.FileName,
                x.FileSize,

                x.DownloadType,
                x.FromVersion,
                x.ToVersion,
                x.FileCount,
                x.Status,
                x.ErrorMessage,

                x.DownloadedAt
            })
            .ToListAsync();

        return Ok(new { page, pageSize, total, totalPages, items });
    }
}
