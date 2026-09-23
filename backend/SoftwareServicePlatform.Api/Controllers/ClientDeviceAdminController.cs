using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 管理员查看和管理客户的设备级自动更新授权。
    ///
    /// 这里管理的是 ClientInstallation：
    /// “一家公司 + 一款软件”下面可以有多台安装设备，
    /// 每台设备拥有自己的 UpdateToken。
    ///
    /// 注意：
    /// 停用这里只会禁止该设备继续检查/下载自动更新，
    /// 不会限制客户启动或正常使用业务软件。
    /// </summary>
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

        /// <summary>
        /// 查看所有“客户 + 软件”授权关系，以及各自已经激活的更新设备数量。
        ///
        /// GET /api/client-device-admin/bindings
        /// </summary>
        [HttpGet("bindings")]
        public async Task<IActionResult> GetBindings(
            CancellationToken cancellationToken)
        {
            var result = await _dbContext.CustomerSoftwares
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Software)
                .OrderBy(x => x.Customer!.Name)
                .ThenBy(x => x.Software!.Name)
                .Select(x => new
                {
                    customerSoftwareId = x.Id,

                    customerId = x.CustomerId,
                    customerName = x.Customer == null
                        ? "-"
                        : x.Customer.Name,
                    customerCode = x.Customer == null
                        ? "-"
                        : x.Customer.Code,
                    customerEnabled = x.Customer != null
                        && x.Customer.IsEnabled,

                    softwareId = x.SoftwareId,
                    softwareName = x.Software == null
                        ? "-"
                        : x.Software.Name,
                    softwareCode = x.Software == null
                        ? "-"
                        : x.Software.Code,
                    softwareEnabled = x.Software != null
                        && x.Software.IsEnabled,

                    bindingEnabled = x.IsEnabled,

                    deviceCount = x.ClientInstallations.Count(),
                    enabledDeviceCount = x.ClientInstallations
                        .Count(d => d.IsEnabled),
                    disabledDeviceCount = x.ClientInstallations
                        .Count(d => !d.IsEnabled),

                    /*
                     * 最近一次任意设备使用 UpdateToken 的时间。
                     * 没有设备或从未检查更新时为 null。
                     */
                    lastUsedAt = x.ClientInstallations
                        .OrderByDescending(d => d.LastUsedAt)
                        .Select(d => d.LastUsedAt)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return Ok(result);
        }

        /// <summary>
        /// 查看某一个“客户 + 软件”授权下面的全部更新设备。
        ///
        /// GET /api/client-device-admin/bindings/{customerSoftwareId}/devices
        /// </summary>
        [HttpGet("bindings/{customerSoftwareId:int}/devices")]
        public async Task<IActionResult> GetDevices(
            int customerSoftwareId,
            CancellationToken cancellationToken)
        {
            var binding = await _dbContext.CustomerSoftwares
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

            var devices = await _dbContext
                .Set<ClientInstallation>()
                .AsNoTracking()
                .Where(x => x.CustomerSoftwareId == customerSoftwareId)
                .OrderByDescending(x => x.ActivatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.InstallationId,
                    x.DeviceName,
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
                customerName = binding.Customer == null
                    ? "-"
                    : binding.Customer.Name,
                customerCode = binding.Customer == null
                    ? "-"
                    : binding.Customer.Code,
                softwareId = binding.SoftwareId,
                softwareName = binding.Software == null
                    ? "-"
                    : binding.Software.Name,
                softwareCode = binding.Software == null
                    ? "-"
                    : binding.Software.Code,
                bindingEnabled = binding.IsEnabled,
                devices
            });
        }

        /// <summary>
        /// 管理员停用一台设备的自动更新权限。
        ///
        /// POST /api/client-device-admin/devices/{id}/revoke
        /// </summary>
        [HttpPost("devices/{id:int}/revoke")]
        public Task<IActionResult> RevokeDevice(
            int id,
            CancellationToken cancellationToken)
        {
            return SetDeviceEnabled(
                id,
                false,
                cancellationToken);
        }

        /// <summary>
        /// 管理员重新启用一台设备的自动更新权限。
        ///
        /// POST /api/client-device-admin/devices/{id}/enable
        /// </summary>
        [HttpPost("devices/{id:int}/enable")]
        public Task<IActionResult> EnableDevice(
            int id,
            CancellationToken cancellationToken)
        {
            return SetDeviceEnabled(
                id,
                true,
                cancellationToken);
        }

        private async Task<IActionResult> SetDeviceEnabled(
            int id,
            bool enabled,
            CancellationToken cancellationToken)
        {
            var device = await _dbContext
                .Set<ClientInstallation>()
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

            if (device == null)
            {
                return NotFound("更新设备不存在");
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
}
