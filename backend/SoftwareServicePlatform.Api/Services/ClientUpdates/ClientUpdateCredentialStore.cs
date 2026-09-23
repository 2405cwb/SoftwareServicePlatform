using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Services.ClientUpdates
{
    /// <summary>
    /// 客户端更新凭证解析器。
    /// 现在只接受 ClientInstallation 的设备级 UpdateToken。
    /// 类名暂时保留以避免大范围修改 ClientUpdatesController。
    /// </summary>
    public sealed class ClientUpdateCredentialStore
    {
        private readonly AppDbContext _dbContext;

        public ClientUpdateCredentialStore(
            AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ClientUpdateCredentialRecord?>
            GetByTokenHashAsync(
                string tokenHash,
                CancellationToken cancellationToken = default)
        {
            var installation =
                await _dbContext.ClientInstallations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.TokenHash == tokenHash,
                        cancellationToken);

            if (installation == null)
            {
                return null;
            }

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

        public async Task TouchLastUsedAsync(
            int credentialId,
            CancellationToken cancellationToken = default)
        {
            if (credentialId >= 0)
            {
                return;
            }

            var installationId = -credentialId;

            var installation =
                await _dbContext.ClientInstallations
                    .FirstOrDefaultAsync(
                        x => x.Id == installationId,
                        cancellationToken);

            if (installation == null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            installation.LastUsedAt = now;
            installation.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
