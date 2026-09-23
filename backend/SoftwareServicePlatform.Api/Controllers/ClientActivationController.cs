using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services.ClientUpdates;
using System.Security.Claims;

namespace SoftwareServicePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/client-activation")]
    public class ClientActivationController : ControllerBase
    {
        private const int ActivationCodeExpireHours = 24;
        private readonly AppDbContext _dbContext;

        public ClientActivationController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("software/{softwareId:int}/code")]
        public async Task<IActionResult> GenerateActivationCode(
            int softwareId,
            CancellationToken cancellationToken)
        {
            if (!TryGetCurrentCustomerId(out var customerId))
            {
                return Forbid();
            }

            var binding =
                await _dbContext.CustomerSoftwares
                    .Include(x => x.Customer)
                    .Include(x => x.Software)
                    .FirstOrDefaultAsync(
                        x => x.CustomerId == customerId
                             && x.SoftwareId == softwareId,
                        cancellationToken);

            if (binding == null)
            {
                return NotFound("当前客户没有该软件授权");
            }

            if (!binding.IsEnabled
                || binding.Customer == null
                || !binding.Customer.IsEnabled
                || binding.Software == null
                || !binding.Software.IsEnabled
                || !binding.Software.AllowDownload)
            {
                return BadRequest("当前客户或软件授权不可用，不能生成更新激活码");
            }

            var activeDeviceCount =
                await _dbContext.ClientInstallations
                    .CountAsync(
                        x => x.CustomerSoftwareId == binding.Id
                             && x.IsEnabled,
                        cancellationToken);

            if (binding.MaxDeviceCount > 0
                && activeDeviceCount >= binding.MaxDeviceCount)
            {
                return BadRequest(
                    $"更新设备数量已达到上限 {binding.MaxDeviceCount} 台。"
                    + "请先停用不再使用的设备，或联系管理员调整设备上限。");
            }

            var clearCode = ClientActivationCodeService.GenerateCode();
            var now = DateTime.UtcNow;

            var entity = new ClientActivationCode
            {
                CustomerSoftwareId = binding.Id,
                CodeHash = ClientActivationCodeService.HashCode(clearCode),
                CreatedByUserId = TryGetCurrentUserId(),
                CreatedAt = now,
                ExpiresAt = now.AddHours(ActivationCodeExpireHours),
                UsedAt = null,
                UsedByInstallationId = string.Empty
            };

            _dbContext.ClientActivationCodes.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                activationCode = clearCode,
                expiresAt = entity.ExpiresAt,
                softwareId = binding.SoftwareId,
                softwareCode = binding.Software.Code,
                softwareName = binding.Software.Name,
                activeDeviceCount,
                maxDeviceCount = binding.MaxDeviceCount,
                message = "一次性更新激活码已生成，请在24小时内用于一台新设备。"
            });
        }

        [Authorize(Roles = "Customer")]
        [HttpGet("software/{softwareId:int}/devices")]
        public async Task<IActionResult> GetDevices(
            int softwareId,
            CancellationToken cancellationToken)
        {
            if (!TryGetCurrentCustomerId(out var customerId))
            {
                return Forbid();
            }

            var binding =
                await _dbContext.CustomerSoftwares
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.CustomerId == customerId
                             && x.SoftwareId == softwareId,
                        cancellationToken);

            if (binding == null)
            {
                return NotFound("当前客户没有该软件授权");
            }

            var devices =
                await _dbContext.ClientInstallations
                    .AsNoTracking()
                    .Where(x => x.CustomerSoftwareId == binding.Id)
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

            /*
             * 保持与当前 MySoftwarePage.tsx 的返回结构兼容：
             * 这里仍直接返回设备数组。
             * 管理员侧设备上限信息由 ClientDeviceAdminController 提供。
             */
            return Ok(devices);
        }

        [Authorize(Roles = "Customer")]
        [HttpPut("devices/{id:int}")]
        public async Task<IActionResult> UpdateDevice(
            int id,
            UpdateClientDeviceRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetCurrentCustomerId(out var customerId))
            {
                return Forbid();
            }

            var device =
                await _dbContext.ClientInstallations
                    .Include(x => x.CustomerSoftware)
                    .FirstOrDefaultAsync(
                        x => x.Id == id
                             && x.CustomerSoftware != null
                             && x.CustomerSoftware.CustomerId == customerId,
                        cancellationToken);

            if (device == null)
            {
                return NotFound("设备不存在或不属于当前客户");
            }

            ApplyDeviceText(device, request.DeviceName, request.Remark);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("devices/{id:int}/revoke")]
        public async Task<IActionResult> RevokeDevice(
            int id,
            CancellationToken cancellationToken)
        {
            if (!TryGetCurrentCustomerId(out var customerId))
            {
                return Forbid();
            }

            var device =
                await _dbContext.ClientInstallations
                    .Include(x => x.CustomerSoftware)
                    .FirstOrDefaultAsync(
                        x => x.Id == id
                             && x.CustomerSoftware != null
                             && x.CustomerSoftware.CustomerId == customerId,
                        cancellationToken);

            if (device == null)
            {
                return NotFound("设备不存在或不属于当前客户");
            }

            device.IsEnabled = false;
            device.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("activate")]
        public async Task<IActionResult> Activate(
            ClientActivateRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ActivationCode))
            {
                return BadRequest("请输入更新激活码");
            }

            if (string.IsNullOrWhiteSpace(request.SoftwareCode))
            {
                return BadRequest("SoftwareCode 不能为空");
            }

            var codeHash =
                ClientActivationCodeService.HashCode(request.ActivationCode);

            var now = DateTime.UtcNow;

            var activationCode =
                await _dbContext.ClientActivationCodes
                    .Include(x => x.CustomerSoftware)
                        .ThenInclude(x => x!.Customer)
                    .Include(x => x.CustomerSoftware)
                        .ThenInclude(x => x!.Software)
                    .FirstOrDefaultAsync(
                        x => x.CodeHash == codeHash,
                        cancellationToken);

            if (activationCode == null)
            {
                return BadRequest("更新激活码无效");
            }

            if (activationCode.UsedAt.HasValue)
            {
                return BadRequest("该更新激活码已经使用，请在网页端重新获取");
            }

            if (activationCode.ExpiresAt <= now)
            {
                return BadRequest("该更新激活码已经过期，请在网页端重新获取");
            }

            var binding = activationCode.CustomerSoftware;

            if (binding == null
                || !binding.IsEnabled
                || binding.Customer == null
                || !binding.Customer.IsEnabled
                || binding.Software == null
                || !binding.Software.IsEnabled
                || !binding.Software.AllowDownload)
            {
                return BadRequest("当前客户或软件更新授权已经停用");
            }

            if (!string.Equals(
                    binding.Software.Code,
                    request.SoftwareCode.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("更新激活码与当前软件不匹配");
            }

            var activeDeviceCount =
                await _dbContext.ClientInstallations
                    .CountAsync(
                        x => x.CustomerSoftwareId == binding.Id
                             && x.IsEnabled,
                        cancellationToken);

            if (binding.MaxDeviceCount > 0
                && activeDeviceCount >= binding.MaxDeviceCount)
            {
                return BadRequest(
                    $"更新设备数量已达到上限 {binding.MaxDeviceCount} 台。"
                    + "请联系管理员或先停用旧设备。");
            }

            var updateToken = ClientUpdateTokenService.GenerateToken();
            var installationId = Guid.NewGuid().ToString("N");

            var deviceName = string.IsNullOrWhiteSpace(request.DeviceName)
                ? "未命名设备"
                : request.DeviceName.Trim();

            if (deviceName.Length > 100)
            {
                deviceName = deviceName[..100];
            }

            var installation = new ClientInstallation
            {
                CustomerSoftwareId = binding.Id,
                InstallationId = installationId,
                DeviceName = deviceName,
                Remark = string.Empty,
                TokenHash = ClientUpdateTokenService.HashToken(updateToken),
                TokenPrefix = ClientUpdateTokenService.GetDisplayPrefix(updateToken),
                IsEnabled = true,
                ActivatedAt = now,
                LastUsedAt = null,
                UpdatedAt = now
            };

            _dbContext.ClientInstallations.Add(installation);

            activationCode.UsedAt = now;
            activationCode.UsedByInstallationId = installationId;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                updateToken,
                installationId,
                deviceName,
                softwareCode = binding.Software.Code,
                softwareName = binding.Software.Name,
                customerName = binding.Customer.Name,
                message = "设备更新授权成功"
            });
        }

        private static void ApplyDeviceText(
            ClientInstallation device,
            string? deviceName,
            string? remark)
        {
            var name = (deviceName ?? string.Empty).Trim();
            var note = (remark ?? string.Empty).Trim();

            if (name.Length > 100)
            {
                name = name[..100];
            }

            if (note.Length > 500)
            {
                note = note[..500];
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                device.DeviceName = name;
            }

            device.Remark = note;
            device.UpdatedAt = DateTime.UtcNow;
        }

        private bool TryGetCurrentCustomerId(out int customerId)
        {
            customerId = 0;
            var value = User.FindFirstValue("customerId");

            return int.TryParse(value, out customerId)
                   && customerId > 0;
        }

        private int? TryGetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(value, out var userId)
                ? userId
                : null;
        }
    }

    public sealed class ClientActivateRequest
    {
        public string ActivationCode { get; set; } = string.Empty;
        public string SoftwareCode { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }

    public sealed class UpdateClientDeviceRequest
    {
        public string DeviceName { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
    }
}
