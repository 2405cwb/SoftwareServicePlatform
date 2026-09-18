using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Services.ClientUpdates;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 客户端自动更新凭证管理。
    ///
    /// 只有 Admin 可以：
    ///
    /// 1. 查看哪些客户软件已经启用自动更新；
    /// 2. 生成 / 重置 UpdateToken；
    /// 3. 停用 / 重新启用 UpdateToken。
    ///
    /// 明文 Token 只在“生成/重置”响应中返回一次。
    /// 数据库只保存 SHA256。
    /// </summary>
    [ApiController]
    [Route("api/client-update-admin")]
    [Authorize(Roles = "Admin")]
    public class ClientUpdateAdminController
        : ControllerBase
    {
        private readonly AppDbContext
            _dbContext;


        public ClientUpdateAdminController(
            AppDbContext dbContext)
        {
            _dbContext =
                dbContext;
        }


        /// <summary>
        /// 查询全部客户软件绑定及其自动更新凭证状态。
        ///
        /// GET /api/client-update-admin/bindings
        /// </summary>
        [HttpGet("bindings")]
        public async Task<IActionResult>
            GetBindings(
                CancellationToken cancellationToken)
        {
            var store =
                new ClientUpdateCredentialStore(
                    _dbContext
                );

            var credentials =
                await store.GetAllAsync(
                    cancellationToken
                );

            var credentialMap =
                credentials
                    .ToDictionary(
                        x => x.CustomerSoftwareId
                    );

            var bindings =
                await _dbContext
                    .CustomerSoftwares
                    .AsNoTracking()
                    .Include(x => x.Customer)
                    .Include(x => x.Software)
                    .OrderBy(
                        x =>
                            x.Customer!.Name
                    )
                    .ThenBy(
                        x =>
                            x.Software!.Name
                    )
                    .ToListAsync(
                        cancellationToken
                    );

            var result =
                bindings.Select(
                    x =>
                    {
                        credentialMap
                            .TryGetValue(
                                x.Id,
                                out var credential
                            );

                        return new
                        {
                            customerSoftwareId =
                                x.Id,

                            x.CustomerId,

                            customerName =
                                x.Customer?.Name
                                ?? string.Empty,

                            customerCode =
                                x.Customer?.Code
                                ?? string.Empty,

                            customerEnabled =
                                x.Customer?.IsEnabled
                                ?? false,

                            x.SoftwareId,

                            softwareName =
                                x.Software?.Name
                                ?? string.Empty,

                            softwareCode =
                                x.Software?.Code
                                ?? string.Empty,

                            softwareEnabled =
                                x.Software?.IsEnabled
                                ?? false,

                            bindingEnabled =
                                x.IsEnabled,

                            credentialExists =
                                credential != null,

                            credentialEnabled =
                                credential?.IsEnabled
                                ?? false,

                            tokenPrefix =
                                credential?.TokenPrefix,

                            tokenCreatedAt =
                                credential?.CreatedAt,

                            tokenUpdatedAt =
                                credential?.UpdatedAt,

                            lastUsedAt =
                                credential?.LastUsedAt
                        };
                    }
                )
                .ToList();

            return Ok(result);
        }


        /// <summary>
        /// 为一个“客户 + 软件”生成或重置 UpdateToken。
        ///
        /// POST
        /// /api/client-update-admin/bindings/{customerSoftwareId}/token
        ///
        /// 返回的 token 只显示这一次。
        /// </summary>
        [HttpPost(
            "bindings/{customerSoftwareId:int}/token")]
        public async Task<IActionResult>
            GenerateToken(
                int customerSoftwareId,
                CancellationToken cancellationToken)
        {
            var binding =
                await _dbContext
                    .CustomerSoftwares
                    .AsNoTracking()
                    .Include(x => x.Customer)
                    .Include(x => x.Software)
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id
                            ==
                            customerSoftwareId,
                        cancellationToken
                    );

            if (binding == null)
            {
                return NotFound(
                    "客户软件绑定不存在"
                );
            }

            if (
                !binding.IsEnabled
                ||
                binding.Customer == null
                ||
                !binding.Customer.IsEnabled
                ||
                binding.Software == null
                ||
                !binding.Software.IsEnabled
            )
            {
                return BadRequest(
                    "客户、软件或授权当前处于停用状态，不能生成更新凭证"
                );
            }

            var token =
                ClientUpdateTokenService
                    .GenerateToken();

            var tokenHash =
                ClientUpdateTokenService
                    .HashToken(token);

            var tokenPrefix =
                ClientUpdateTokenService
                    .GetDisplayPrefix(
                        token
                    );

            var store =
                new ClientUpdateCredentialStore(
                    _dbContext
                );

            await store.UpsertAsync(
                customerSoftwareId,
                tokenHash,
                tokenPrefix,
                cancellationToken
            );

            return Ok(
                new
                {
                    message =
                        "更新凭证已生成。请立即复制，关闭后无法再次查看完整 Token。",

                    updateToken =
                        token,

                    bindingId =
                        customerSoftwareId,

                    customerName =
                        binding.Customer.Name,

                    softwareName =
                        binding.Software.Name,

                    softwareCode =
                        binding.Software.Code
                }
            );
        }


        /// <summary>
        /// 停用现有 UpdateToken。
        ///
        /// 停用后客户端立即失去更新检查和更新文件下载权限。
        /// </summary>
        [HttpPost(
            "bindings/{customerSoftwareId:int}/revoke")]
        public async Task<IActionResult>
            RevokeToken(
                int customerSoftwareId,
                CancellationToken cancellationToken)
        {
            var store =
                new ClientUpdateCredentialStore(
                    _dbContext
                );

            var credential =
                await store
                    .GetByCustomerSoftwareIdAsync(
                        customerSoftwareId,
                        cancellationToken
                    );

            if (credential == null)
            {
                return NotFound(
                    "该客户软件尚未生成更新凭证"
                );
            }

            await store.SetEnabledAsync(
                customerSoftwareId,
                enabled: false,
                cancellationToken
            );

            return NoContent();
        }


        /// <summary>
        /// 重新启用之前停用的 UpdateToken。
        ///
        /// 注意：
        /// 只有管理员仍然知道客户端原有明文 Token 时才有意义。
        /// 如果 Token 已丢失，应直接“重新生成”。
        /// </summary>
        [HttpPost(
            "bindings/{customerSoftwareId:int}/enable")]
        public async Task<IActionResult>
            EnableToken(
                int customerSoftwareId,
                CancellationToken cancellationToken)
        {
            var bindingExists =
                await _dbContext
                    .CustomerSoftwares
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.Id
                            ==
                            customerSoftwareId
                            &&
                            x.IsEnabled
                            &&
                            x.Customer != null
                            &&
                            x.Customer.IsEnabled
                            &&
                            x.Software != null
                            &&
                            x.Software.IsEnabled,
                        cancellationToken
                    );

            if (!bindingExists)
            {
                return BadRequest(
                    "客户、软件或授权当前不可用"
                );
            }

            var store =
                new ClientUpdateCredentialStore(
                    _dbContext
                );

            var credential =
                await store
                    .GetByCustomerSoftwareIdAsync(
                        customerSoftwareId,
                        cancellationToken
                    );

            if (credential == null)
            {
                return NotFound(
                    "该客户软件尚未生成更新凭证"
                );
            }

            await store.SetEnabledAsync(
                customerSoftwareId,
                enabled: true,
                cancellationToken
            );

            return NoContent();
        }
    }
}
