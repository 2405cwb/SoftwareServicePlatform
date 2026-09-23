using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/client-device-admin")]
    [Authorize(Roles = "Admin")]
    public class ClientDeviceAdminController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public ClientDeviceAdminController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("bindings")]
        public async Task<IActionResult> GetBindings(
            CancellationToken cancellationToken)
        {
            var result =
                await _dbContext.CustomerSoftwares
                    .AsNoTracking()
                    .Include(x => x.Customer)
                    .Include(x => x.Software)
                    .OrderBy(x => x.Customer!.Name)
                    .ThenBy(x => x.Software!.Name)
                    .Select(x => new
                    {
                        customerSoftwareId = x.Id,
                        customerId = x.CustomerId,
                        customerName = x.Customer == null ? "-" : x.Customer.Name,
                        customerCode = x.Customer == null ? "-" : x.Customer.Code,
                        customerEnabled = x.Customer != null && x.Customer.IsEnabled,
                        softwareId = x.SoftwareId,
                        softwareName = x.Software == null ? "-" : x.Software.Name,
                        softwareCode = x.Software == null ? "-" : x.Software.Code,
                        softwareEnabled = x.Software != null && x.Software.IsEnabled,
                        bindingEnabled = x.IsEnabled,
                        maxDeviceCount = x.MaxDeviceCount,
                        deviceCount = x.ClientInstallations.Count(),
                        enabledDeviceCount = x.ClientInstallations.Count(d => d.IsEnabled),
                        disabledDeviceCount = x.ClientInstallations.Count(d => !d.IsEnabled),
                        lastUsedAt = x.ClientInstallations
                            .OrderByDescending(d => d.LastUsedAt)
                            .Select(d => d.LastUsedAt)
                            .FirstOrDefault()
                    })
                    .ToListAsync(cancellationToken);

            return Ok(result);
        }

        [HttpGet("bindings/{customerSoftwareId:int}/devices")]
        public async Task<IActionResult> GetDevices(
            int customerSoftwareId,
            CancellationToken cancellationToken)
        {
            var binding =
                await _dbContext.CustomerSoftwares
                    .AsNoTracking()
                    .Include(x => x.Customer)
                    .Include(x => x.Software)
                    .FirstOrDefaultAsync(
                        x => x.Id == customerSoftwareId,
                        cancellationToken);

            if (binding == null)
            {
                return NotFound("客户软件授权关系不存在");
            }

            var devices =
                await _dbContext.ClientInstallations
                    .AsNoTracking()
                    .Where(x => x.CustomerSoftwareId == customerSoftwareId)
                    .OrderByDescending(x => x.ActivatedAt)
                    .Select(x => new
                    {
                        x.Id,
                        x.InstallationId,
                        x.DeviceName,
                        x.Remark,
                        x.TokenPrefix,
                        x.IsEnabled,
                        x.ActivatedAt,
                        x.LastUsedAt,
                        x.UpdatedAt
                    })
                    .ToListAsync(cancellationToken);

            return Ok(new
            {
                customerSoftwareId = binding.Id,
                customerId = binding.CustomerId,
                customerName = binding.Customer?.Name ?? "-",
                customerCode = binding.Customer?.Code ?? "-",
                softwareId = binding.SoftwareId,
                softwareName = binding.Software?.Name ?? "-",
                softwareCode = binding.Software?.Code ?? "-",
                bindingEnabled = binding.IsEnabled,
                maxDeviceCount = binding.MaxDeviceCount,
                enabledDeviceCount = devices.Count(x => x.IsEnabled),
                devices
            });
        }

        [HttpPut("bindings/{customerSoftwareId:int}/device-limit")]
        public async Task<IActionResult> SetDeviceLimit(
            int customerSoftwareId,
            SetDeviceLimitRequest request,
            CancellationToken cancellationToken)
        {
            if (request.MaxDeviceCount < 0 || request.MaxDeviceCount > 9999)
            {
                return BadRequest("设备上限必须在 0～9999 之间，0 表示不限制");
            }

            var binding =
                await _dbContext.CustomerSoftwares
                    .FirstOrDefaultAsync(
                        x => x.Id == customerSoftwareId,
                        cancellationToken);

            if (binding == null)
            {
                return NotFound("客户软件授权关系不存在");
            }

            binding.MaxDeviceCount = request.MaxDeviceCount;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var activeCount =
                await _dbContext.ClientInstallations
                    .CountAsync(
                        x => x.CustomerSoftwareId == customerSoftwareId
                             && x.IsEnabled,
                        cancellationToken);

            return Ok(new
            {
                message = request.MaxDeviceCount == 0
                    ? "更新设备数量已设置为不限制"
                    : $"更新设备上限已设置为 {request.MaxDeviceCount} 台",
                activeDeviceCount = activeCount,
                maxDeviceCount = request.MaxDeviceCount
            });
        }

        [HttpPut("devices/{id:int}")]
        public async Task<IActionResult> UpdateDevice(
            int id,
            UpdateDeviceAdminRequest request,
            CancellationToken cancellationToken)
        {
            var device =
                await _dbContext.ClientInstallations
                    .FirstOrDefaultAsync(
                        x => x.Id == id,
                        cancellationToken);

            if (device == null)
            {
                return NotFound("更新设备不存在");
            }

            var name = (request.DeviceName ?? string.Empty).Trim();
            var remark = (request.Remark ?? string.Empty).Trim();

            if (name.Length > 100)
            {
                return BadRequest("设备名称不能超过100个字符");
            }

            if (remark.Length > 500)
            {
                return BadRequest("设备备注不能超过500个字符");
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                device.DeviceName = name;
            }

            device.Remark = remark;
            device.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [HttpPost("devices/{id:int}/revoke")]
        public Task<IActionResult> RevokeDevice(
            int id,
            CancellationToken cancellationToken)
            => SetDeviceEnabled(id, false, cancellationToken);

        [HttpPost("devices/{id:int}/enable")]
        public Task<IActionResult> EnableDevice(
            int id,
            CancellationToken cancellationToken)
            => SetDeviceEnabled(id, true, cancellationToken);

        private async Task<IActionResult> SetDeviceEnabled(
            int id,
            bool enabled,
            CancellationToken cancellationToken)
        {
            var device =
                await _dbContext.ClientInstallations
                    .Include(x => x.CustomerSoftware)
                    .FirstOrDefaultAsync(
                        x => x.Id == id,
                        cancellationToken);

            if (device == null)
            {
                return NotFound("更新设备不存在");
            }

            if (enabled
                && device.CustomerSoftware != null
                && device.CustomerSoftware.MaxDeviceCount > 0)
            {
                var activeCount =
                    await _dbContext.ClientInstallations
                        .CountAsync(
                            x => x.CustomerSoftwareId == device.CustomerSoftwareId
                                 && x.IsEnabled
                                 && x.Id != device.Id,
                            cancellationToken);

                if (activeCount >= device.CustomerSoftware.MaxDeviceCount)
                {
                    return BadRequest(
                        $"当前启用设备数已达到上限 "
                        + $"{device.CustomerSoftware.MaxDeviceCount} 台");
                }
            }

            device.IsEnabled = enabled;
            device.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = enabled
                    ? "已重新启用该设备的自动更新权限"
                    : "已停用该设备的自动更新权限"
            });
        }
    }

    public sealed class SetDeviceLimitRequest
    {
        public int MaxDeviceCount { get; set; }
    }

    public sealed class UpdateDeviceAdminRequest
    {
        public string DeviceName { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
    }
}
