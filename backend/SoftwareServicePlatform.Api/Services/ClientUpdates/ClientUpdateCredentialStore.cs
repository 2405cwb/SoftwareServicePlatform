using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Services.ClientUpdates
{
    /// <summary>
    /// 客户端更新凭证存储。
    ///
    /// 兼容两套凭证来源：
    /// 1. 旧版 ClientUpdateCredential：一个“客户 + 软件”共享一套 Token；
    /// 2. 新版 ClientInstallation：每一个安装实例拥有独立 Token。
    ///
    /// 新客户端应统一使用 ClientInstallation。
    /// 旧版共享 Token 仅为了平滑迁移继续保留，
    /// 等客户全部完成设备激活后可以在后台逐步停用。
    /// </summary>
    public sealed class ClientUpdateCredentialStore
    {
        private readonly AppDbContext _dbContext;

        public ClientUpdateCredentialStore(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 旧版管理页面继续读取旧版共享凭证。
        ///
        /// 新的设备凭证由“我的软件 -> 设备管理”维护，
        /// 不混在旧版共享凭证列表中。
        /// </summary>
        public async Task<List<ClientUpdateCredentialRecord>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.ClientUpdateCredentials
                .AsNoTracking()
                .OrderBy(x => x.CustomerSoftwareId)
                .Select(x => new ClientUpdateCredentialRecord
                {
                    Id = x.Id,
                    CustomerSoftwareId = x.CustomerSoftwareId,
                    TokenHash = x.TokenHash,
                    TokenPrefix = x.TokenPrefix,
                    IsEnabled = x.IsEnabled,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    LastUsedAt = x.LastUsedAt
                })
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 旧版“客户 + 软件”共享凭证查询。
        /// </summary>
        public async Task<ClientUpdateCredentialRecord?>
            GetByCustomerSoftwareIdAsync(
                int customerSoftwareId,
                CancellationToken cancellationToken = default)
        {
            return await _dbContext.ClientUpdateCredentials
                .AsNoTracking()
                .Where(x => x.CustomerSoftwareId == customerSoftwareId)
                .Select(x => new ClientUpdateCredentialRecord
                {
                    Id = x.Id,
                    CustomerSoftwareId = x.CustomerSoftwareId,
                    TokenHash = x.TokenHash,
                    TokenPrefix = x.TokenPrefix,
                    IsEnabled = x.IsEnabled,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    LastUsedAt = x.LastUsedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// 根据 TokenHash 查找更新凭证。
        ///
        /// 查询顺序：
        /// 新版设备凭证 -> 旧版共享凭证。
        ///
        /// 这样新的客户端可以直接使用设备独立 Token，
        /// 同时不会突然让已经部署出去的旧客户端失效。
        /// </summary>
        public async Task<ClientUpdateCredentialRecord?> GetByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            var installation = await _dbContext.Set<ClientInstallation>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TokenHash == tokenHash,
                    cancellationToken
                );

            if (installation != null)
            {
                /*
                 * ClientUpdatesController 现有代码只认识
                 * ClientUpdateCredentialRecord，
                 * 因此这里把设备凭证映射成同一个轻量模型。
                 *
                 * Id 使用负数只是内部标记：
                 * TouchLastUsedAsync 收到负数时就知道
                 * 应该更新 ClientInstallation，而不是旧表。
                 */
                return new ClientUpdateCredentialRecord
                {
                    Id = -installation.Id,
                    CustomerSoftwareId = installation.CustomerSoftwareId,
                    TokenHash = installation.TokenHash,
                    TokenPrefix = installation.TokenPrefix,
                    IsEnabled = installation.IsEnabled,
                    CreatedAt = installation.ActivatedAt,
                    UpdatedAt = installation.UpdatedAt,
                    LastUsedAt = installation.LastUsedAt
                };
            }

            return await _dbContext.ClientUpdateCredentials
                .AsNoTracking()
                .Where(x => x.TokenHash == tokenHash)
                .Select(x => new ClientUpdateCredentialRecord
                {
                    Id = x.Id,
                    CustomerSoftwareId = x.CustomerSoftwareId,
                    TokenHash = x.TokenHash,
                    TokenPrefix = x.TokenPrefix,
                    IsEnabled = x.IsEnabled,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    LastUsedAt = x.LastUsedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// 旧版后台第一次生成 Token 时新增；
        /// 重置 Token 时覆盖旧 Hash。
        ///
        /// 注意：
        /// 新客户端不再调用这里生成设备凭证，
        /// 而是通过一次性激活码创建 ClientInstallation。
        /// </summary>
        public async Task UpsertAsync(
            int customerSoftwareId,
            string tokenHash,
            string tokenPrefix,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.ClientUpdateCredentials
                .FirstOrDefaultAsync(
                    x => x.CustomerSoftwareId == customerSoftwareId,
                    cancellationToken
                );

            var now = DateTime.UtcNow;

            if (entity == null)
            {
                entity = new ClientUpdateCredential
                {
                    CustomerSoftwareId = customerSoftwareId,
                    TokenHash = tokenHash,
                    TokenPrefix = tokenPrefix,
                    IsEnabled = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastUsedAt = null
                };

                _dbContext.ClientUpdateCredentials.Add(entity);
            }
            else
            {
                entity.TokenHash = tokenHash;
                entity.TokenPrefix = tokenPrefix;
                entity.IsEnabled = true;
                entity.UpdatedAt = now;
                entity.LastUsedAt = null;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// 旧版共享凭证启停。
        /// </summary>
        public async Task SetEnabledAsync(
            int customerSoftwareId,
            bool enabled,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.ClientUpdateCredentials
                .FirstOrDefaultAsync(
                    x => x.CustomerSoftwareId == customerSoftwareId,
                    cancellationToken
                );

            if (entity == null)
            {
                return;
            }

            entity.IsEnabled = enabled;
            entity.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// 更新最近使用时间。
        ///
        /// credentialId > 0：旧版 ClientUpdateCredential；
        /// credentialId < 0：新版 ClientInstallation。
        /// </summary>
        public async Task TouchLastUsedAsync(
            int credentialId,
            CancellationToken cancellationToken = default)
        {
            if (credentialId < 0)
            {
                var installationId = -credentialId;

                var installation = await _dbContext.Set<ClientInstallation>()
                    .FirstOrDefaultAsync(
                        x => x.Id == installationId,
                        cancellationToken
                    );

                if (installation == null)
                {
                    return;
                }

                installation.LastUsedAt = DateTime.UtcNow;
                installation.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var entity = await _dbContext.ClientUpdateCredentials
                .FirstOrDefaultAsync(
                    x => x.Id == credentialId,
                    cancellationToken
                );

            if (entity == null)
            {
                return;
            }

            entity.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
