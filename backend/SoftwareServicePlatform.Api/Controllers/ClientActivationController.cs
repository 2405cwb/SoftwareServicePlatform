using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services.ClientUpdates;
using System.Security.Claims;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 客户端首次激活与设备管理。
    ///
    /// 设计：
    /// CustomerSoftware = 公司级软件授权；
    /// ClientActivationCode = 一次性更新激活码；
    /// ClientInstallation = 一台设备上的一个安装实例；
    /// 每个 ClientInstallation 都拥有自己独立的 UpdateToken。
    /// </summary>
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

        /// <summary>
        /// 客户在网页端为自己已经授权的软件生成一次性更新激活码。
        ///
        /// POST /api/client-activation/software/{softwareId}/code
        /// </summary>
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

            var binding = await _dbContext.CustomerSoftwares
                .AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Software)
                .FirstOrDefaultAsync(
                    x =>
                        x.CustomerId == customerId
                        && x.SoftwareId == softwareId,
                    cancellationToken
                );

            if (binding == null)
            {
                return NotFound("当前客户没有该软件授权");
            }

            if (
                !binding.IsEnabled
                || binding.Customer == null
                || !binding.Customer.IsEnabled
                || binding.Software == null
                || !binding.Software.IsEnabled
                || !binding.Software.AllowDownload
            )
            {
                return BadRequest("当前客户或软件授权不可用，不能生成更新激活码");
            }

            var clearCode = ClientActivationCodeService.GenerateCode();
            var codeHash = ClientActivationCodeService.HashCode(clearCode);
            var now = DateTime.UtcNow;

            var userId = TryGetCurrentUserId();

            var entity = new ClientActivationCode
            {
                CustomerSoftwareId = binding.Id,
                CodeHash = codeHash,
                CreatedByUserId = userId,
                CreatedAt = now,
                ExpiresAt = now.AddHours(ActivationCodeExpireHours),
                UsedAt = null,
                UsedByInstallationId = string.Empty
            };

            _dbContext.Set<ClientActivationCode>().Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            /*
             * 明文更新激活码只在这里返回一次。
             * 数据库只保存 SHA256，关闭弹窗后服务器无法恢复明文。
             */
            return Ok(new
            {
                activationCode = clearCode,
                expiresAt = entity.ExpiresAt,
                softwareId = binding.SoftwareId,
                softwareCode = binding.Software.Code,
                softwareName = binding.Software.Name,
                message = "一次性更新激活码已生成，请在24小时内为一台新设备开通自动更新权限。"
            });
        }

        /// <summary>
        /// 客户查看某款软件已经激活的设备。
        ///
        /// GET /api/client-activation/software/{softwareId}/devices
        /// </summary>
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

            var binding = await _dbContext.CustomerSoftwares
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.CustomerId == customerId
                        && x.SoftwareId == softwareId,
                    cancellationToken
                );

            if (binding == null)
            {
                return NotFound("当前客户没有该软件授权");
            }

            var devices = await _dbContext.Set<ClientInstallation>()
                .AsNoTracking()
                .Where(x => x.CustomerSoftwareId == binding.Id)
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

            return Ok(devices);
        }

        /// <summary>
        /// 客户停用自己某一台设备的更新权限。
        ///
        /// POST /api/client-activation/devices/{id}/revoke
        ///
        /// 只影响这一台设备，其他设备的 UpdateToken 不受影响。
        /// </summary>
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

            var installation = await _dbContext.Set<ClientInstallation>()
                .Include(x => x.CustomerSoftware)
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id
                        && x.CustomerSoftware != null
                        && x.CustomerSoftware.CustomerId == customerId,
                    cancellationToken
                );

            if (installation == null)
            {
                return NotFound("设备不存在或不属于当前客户");
            }

            installation.IsEnabled = false;
            installation.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// 桌面软件第一次启动时使用一次性更新激活码换取设备 UpdateToken。
        ///
        /// POST /api/client-activation/activate
        ///
        /// 本接口不使用网页登录 JWT，
        /// 因为新安装的软件此时还没有任何登录状态。
        /// </summary>
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

            var codeHash = ClientActivationCodeService.HashCode(
                request.ActivationCode
            );

            var now = DateTime.UtcNow;

            var activationCode = await _dbContext.Set<ClientActivationCode>()
                .Include(x => x.CustomerSoftware)
                    .ThenInclude(x => x!.Customer)
                .Include(x => x.CustomerSoftware)
                    .ThenInclude(x => x!.Software)
                .FirstOrDefaultAsync(
                    x => x.CodeHash == codeHash,
                    cancellationToken
                );

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

            if (
                binding == null
                || !binding.IsEnabled
                || binding.Customer == null
                || !binding.Customer.IsEnabled
                || binding.Software == null
                || !binding.Software.IsEnabled
                || !binding.Software.AllowDownload
            )
            {
                return BadRequest("当前客户或软件授权已经停用");
            }

            if (!string.Equals(
                    binding.Software.Code,
                    request.SoftwareCode.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("更新激活码与当前软件不匹配");
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
                TokenHash = ClientUpdateTokenService.HashToken(updateToken),
                TokenPrefix = ClientUpdateTokenService.GetDisplayPrefix(updateToken),
                IsEnabled = true,
                ActivatedAt = now,
                LastUsedAt = null,
                UpdatedAt = now
            };

            _dbContext.Set<ClientInstallation>().Add(installation);

            /*
             * 更新激活码严格一次性使用。
             * 一旦生成设备 UpdateToken，就立即标记已使用。
             */
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

        private bool TryGetCurrentCustomerId(out int customerId)
        {
            customerId = 0;

            var customerIdText = User.FindFirstValue("customerId");

            return int.TryParse(customerIdText, out customerId)
                   && customerId > 0;
        }

        private int? TryGetCurrentUserId()
        {
            var userIdText = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            return int.TryParse(userIdText, out var userId)
                ? userId
                : null;
        }
    }

    /// <summary>
    /// 桌面客户端首次激活请求。
    /// </summary>
    public sealed class ClientActivateRequest
    {
        public string ActivationCode { get; set; } = string.Empty;

        public string SoftwareCode { get; set; } = string.Empty;

        /// <summary>
        /// 客户端建议上传 Environment.MachineName，
        /// 只用于网页上帮助用户识别设备。
        /// </summary>
        public string DeviceName { get; set; } = string.Empty;
    }
}
