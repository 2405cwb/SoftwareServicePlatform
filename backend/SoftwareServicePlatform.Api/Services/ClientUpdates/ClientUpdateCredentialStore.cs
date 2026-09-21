using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Services.ClientUpdates
{
    /// <summary>
    /// 客户端更新凭证存储。
    ///
    /// ClientUpdateCredentials 已正式由
    /// EF Core Entity + Migration 管理。
    /// </summary>
    public sealed class ClientUpdateCredentialStore
    {
        private readonly AppDbContext
            _dbContext;


        public ClientUpdateCredentialStore(
            AppDbContext dbContext)
        {
            _dbContext =
                dbContext;
        }


        public async Task<
            List<ClientUpdateCredentialRecord>>
            GetAllAsync(
                CancellationToken cancellationToken =
                    default)
        {
            return await _dbContext
                .ClientUpdateCredentials
                .AsNoTracking()
                .OrderBy(
                    x => x.CustomerSoftwareId
                )
                .Select(
                    x =>
                        new ClientUpdateCredentialRecord
                        {
                            Id =
                                x.Id,

                            CustomerSoftwareId =
                                x.CustomerSoftwareId,

                            TokenHash =
                                x.TokenHash,

                            TokenPrefix =
                                x.TokenPrefix,

                            IsEnabled =
                                x.IsEnabled,

                            CreatedAt =
                                x.CreatedAt,

                            UpdatedAt =
                                x.UpdatedAt,

                            LastUsedAt =
                                x.LastUsedAt
                        }
                )
                .ToListAsync(
                    cancellationToken
                );
        }


        public async Task<
            ClientUpdateCredentialRecord?>
            GetByCustomerSoftwareIdAsync(
                int customerSoftwareId,
                CancellationToken cancellationToken =
                    default)
        {
            return await _dbContext
                .ClientUpdateCredentials
                .AsNoTracking()
                .Where(
                    x =>
                        x.CustomerSoftwareId
                        ==
                        customerSoftwareId
                )
                .Select(
                    x =>
                        new ClientUpdateCredentialRecord
                        {
                            Id =
                                x.Id,

                            CustomerSoftwareId =
                                x.CustomerSoftwareId,

                            TokenHash =
                                x.TokenHash,

                            TokenPrefix =
                                x.TokenPrefix,

                            IsEnabled =
                                x.IsEnabled,

                            CreatedAt =
                                x.CreatedAt,

                            UpdatedAt =
                                x.UpdatedAt,

                            LastUsedAt =
                                x.LastUsedAt
                        }
                )
                .FirstOrDefaultAsync(
                    cancellationToken
                );
        }


        public async Task<
            ClientUpdateCredentialRecord?>
            GetByTokenHashAsync(
                string tokenHash,
                CancellationToken cancellationToken =
                    default)
        {
            return await _dbContext
                .ClientUpdateCredentials
                .AsNoTracking()
                .Where(
                    x =>
                        x.TokenHash
                        ==
                        tokenHash
                )
                .Select(
                    x =>
                        new ClientUpdateCredentialRecord
                        {
                            Id =
                                x.Id,

                            CustomerSoftwareId =
                                x.CustomerSoftwareId,

                            TokenHash =
                                x.TokenHash,

                            TokenPrefix =
                                x.TokenPrefix,

                            IsEnabled =
                                x.IsEnabled,

                            CreatedAt =
                                x.CreatedAt,

                            UpdatedAt =
                                x.UpdatedAt,

                            LastUsedAt =
                                x.LastUsedAt
                        }
                )
                .FirstOrDefaultAsync(
                    cancellationToken
                );
        }


        /// <summary>
        /// 第一次生成 Token 时新增；
        /// 重置 Token 时覆盖旧 Hash。
        /// </summary>
        public async Task UpsertAsync(
            int customerSoftwareId,
            string tokenHash,
            string tokenPrefix,
            CancellationToken cancellationToken =
                default)
        {
            var entity =
                await _dbContext
                    .ClientUpdateCredentials
                    .FirstOrDefaultAsync(
                        x =>
                            x.CustomerSoftwareId
                            ==
                            customerSoftwareId,
                        cancellationToken
                    );

            var now =
                DateTime.UtcNow;


            if (entity == null)
            {
                entity =
                    new ClientUpdateCredential
                    {
                        CustomerSoftwareId =
                            customerSoftwareId,

                        TokenHash =
                            tokenHash,

                        TokenPrefix =
                            tokenPrefix,

                        IsEnabled =
                            true,

                        CreatedAt =
                            now,

                        UpdatedAt =
                            now,

                        LastUsedAt =
                            null
                    };

                _dbContext
                    .ClientUpdateCredentials
                    .Add(
                        entity
                    );
            }
            else
            {
                entity.TokenHash =
                    tokenHash;

                entity.TokenPrefix =
                    tokenPrefix;

                entity.IsEnabled =
                    true;

                entity.UpdatedAt =
                    now;

                entity.LastUsedAt =
                    null;
            }


            await _dbContext
                .SaveChangesAsync(
                    cancellationToken
                );
        }


        public async Task SetEnabledAsync(
            int customerSoftwareId,
            bool enabled,
            CancellationToken cancellationToken =
                default)
        {
            var entity =
                await _dbContext
                    .ClientUpdateCredentials
                    .FirstOrDefaultAsync(
                        x =>
                            x.CustomerSoftwareId
                            ==
                            customerSoftwareId,
                        cancellationToken
                    );

            if (entity == null)
            {
                return;
            }

            entity.IsEnabled =
                enabled;

            entity.UpdatedAt =
                DateTime.UtcNow;


            await _dbContext
                .SaveChangesAsync(
                    cancellationToken
                );
        }


        public async Task TouchLastUsedAsync(
            int credentialId,
            CancellationToken cancellationToken =
                default)
        {
            var entity =
                await _dbContext
                    .ClientUpdateCredentials
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id
                            ==
                            credentialId,
                        cancellationToken
                    );

            if (entity == null)
            {
                return;
            }

            entity.LastUsedAt =
                DateTime.UtcNow;


            await _dbContext
                .SaveChangesAsync(
                    cancellationToken
                );
        }
    }
}