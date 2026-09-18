using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services.ClientUpdates;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 桌面客户端自动更新接口。
    ///
    /// 本 Controller 不使用平台 JWT，
    /// 而使用专门的：
    ///
    /// X-Update-Token
    ///
    /// 这个 Token 只代表：
    ///
    /// 某个客户
    /// +
    /// 某个软件
    ///
    /// 的更新权限。
    ///
    /// 它不能访问工单、客户、用户等其它平台数据。
    /// </summary>
    [ApiController]
    [Route("api/client-updates")]
    [AllowAnonymous]
    public class ClientUpdatesController
        : ControllerBase
    {
        private const string
            UpdateTokenHeader =
                "X-Update-Token";


        private readonly AppDbContext
            _dbContext;

        private readonly IWebHostEnvironment
            _environment;


        public ClientUpdatesController(
            AppDbContext dbContext,
            IWebHostEnvironment environment)
        {
            _dbContext =
                dbContext;

            _environment =
                environment;
        }


        /// <summary>
        /// 检查当前客户端是否存在新版本。
        ///
        /// POST /api/client-updates/check
        ///
        /// Header:
        ///
        /// X-Update-Token: ssp_upd_xxx
        ///
        /// Body:
        ///
        /// {
        ///   "softwareCode": "ROAD_PROCESS",
        ///   "currentVersion": "1.0.0"
        /// }
        /// </summary>
        [HttpPost("check")]
        public async Task<IActionResult>
            Check(
                ClientUpdateCheckRequest request,
                CancellationToken cancellationToken)
        {
            if (
                string.IsNullOrWhiteSpace(
                    request.SoftwareCode)
            )
            {
                return BadRequest(
                    "SoftwareCode 不能为空"
                );
            }

            if (
                string.IsNullOrWhiteSpace(
                    request.CurrentVersion)
            )
            {
                return BadRequest(
                    "CurrentVersion 不能为空"
                );
            }

            var resolved =
                await ResolveCredentialAsync(
                    request.SoftwareCode,
                    cancellationToken
                );

            if (resolved == null)
            {
                return Unauthorized(
                    new
                    {
                        message =
                            "更新凭证无效、已停用，或当前客户/软件授权不可用"
                    }
                );
            }

            var (
                credential,
                binding) =
                    resolved.Value;

            /*
             * 当前客户真正有权获得的版本，
             * 必须出现在 SoftwareVersionCustomers 发布快照中。
             *
             * 不能只看“这个软件最新版本”，
             * 因为 Beta / 灰度发布可能只发给部分客户。
             */
            var publishedVersionIds =
                await _dbContext
                    .SoftwareVersionCustomers
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.CustomerId
                            ==
                            binding.CustomerId
                    )
                    .Select(
                        x =>
                            x.SoftwareVersionId
                    )
                    .ToListAsync(
                        cancellationToken
                    );

            var candidates =
                await _dbContext
                    .SoftwareVersions
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.SoftwareId
                            ==
                            binding.SoftwareId
                            &&
                            publishedVersionIds
                                .Contains(x.Id)
                            &&
                            x.PublishStatus
                            ==
                            "Published"
                            &&
                            x.IsPublished
                            &&
                            x.AllowDownload
                    )
                    .ToListAsync(
                        cancellationToken
                    );

            SoftwareVersion?
                latest =
                    null;

            foreach (
                var candidate
                in candidates)
            {
                if (
                    latest == null
                    ||
                    ClientVersionComparer
                        .Compare(
                            candidate.Version,
                            latest.Version
                        )
                        > 0
                )
                {
                    latest =
                        candidate;
                }
            }

            /*
             * 有授权，但是当前还没有发布给该客户的版本。
             */
            if (latest == null)
            {
                await TouchCredentialAsync(
                    credential,
                    cancellationToken
                );

                return Ok(
                    new ClientUpdateCheckResponse
                    {
                        HasUpdate =
                            false,

                        CurrentVersion =
                            request.CurrentVersion
                    }
                );
            }

            var hasUpdate =
                ClientVersionComparer
                    .Compare(
                        latest.Version,
                        request.CurrentVersion
                    )
                > 0;

            await TouchCredentialAsync(
                credential,
                cancellationToken
            );

            if (!hasUpdate)
            {
                return Ok(
                    new ClientUpdateCheckResponse
                    {
                        HasUpdate =
                            false,

                        CurrentVersion =
                            request.CurrentVersion,

                        VersionId =
                            latest.Id,

                        LatestVersion =
                            latest.Version
                    }
                );
            }

            var packageStore =
                new ClientUpdatePackageStore(
                    _environment.ContentRootPath
                );

            var manifest =
                packageStore.LoadManifest(
                    latest.Id
                );

            /*
             * 草稿阶段如果上传更新 ZIP 后又修改了版本号/所属软件，
             * 旧 manifest 不能继续使用。
             *
             * 正式发布时如果没有重新上传，
             * 客户端会自动回退完整安装包，而不会拿错增量文件。
             */
            if (
                manifest != null
                &&
                (
                    manifest.SoftwareId
                        != latest.SoftwareId
                    ||
                    !string.Equals(
                        manifest.Version,
                        latest.Version,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                manifest = null;
            }

            var fullPackageExists =
                TryGetFullPackagePath(
                    latest,
                    out _
                );

            return Ok(
                new ClientUpdateCheckResponse
                {
                    HasUpdate =
                        true,

                    CurrentVersion =
                        request.CurrentVersion,

                    VersionId =
                        latest.Id,

                    LatestVersion =
                        latest.Version,

                    Title =
                        latest.Title,

                    ReleaseNotes =
                        latest.ReleaseNotes,

                    ForceUpdate =
                        latest.ForceUpdate,

                    PublishedAt =
                        latest.PublishedAt,

                    IncrementalAvailable =
                        manifest != null,

                    IncrementalTotalSize =
                        manifest?.TotalFileSize
                        ?? 0,

                    FullPackageAvailable =
                        fullPackageExists,

                    FullPackageFileName =
                        latest.PackageFileName,

                    FullPackageFileSize =
                        latest.PackageFileSize,

                    FullPackageSha256 =
                        latest.PackageSha256,

                    Manifest =
                        manifest
                }
            );
        }


        /// <summary>
        /// 下载目标版本中的一个增量文件。
        ///
        /// GET
        /// /api/client-updates/versions/{versionId}/files?path=xxx.dll
        ///
        /// 必须携带同一个 X-Update-Token。
        /// </summary>
        [HttpGet(
            "versions/{versionId:int}/files")]
        public async Task<IActionResult>
            DownloadIncrementalFile(
                int versionId,
                [FromQuery] string path,
                CancellationToken cancellationToken)
        {
            var resolved =
                await ResolveCredentialAsync(
                    softwareCode: null,
                    cancellationToken
                );

            if (resolved == null)
            {
                return Unauthorized();
            }

            var (
                _,
                binding) =
                    resolved.Value;

            var version =
                await GetAuthorizedVersionAsync(
                    versionId,
                    binding.CustomerId,
                    binding.SoftwareId,
                    cancellationToken
                );

            if (version == null)
            {
                return Forbid();
            }

            var packageStore =
                new ClientUpdatePackageStore(
                    _environment.ContentRootPath
                );

            var manifest =
                packageStore.LoadManifest(
                    versionId
                );

            if (manifest == null)
            {
                return NotFound(
                    "当前版本没有增量更新包"
                );
            }

            var normalizedPath =
                ClientUpdatePackageStore
                    .NormalizeRelativePath(
                        path
                    );

            var manifestFile =
                manifest.Files
                    .FirstOrDefault(
                        x =>
                            string.Equals(
                                x.Path,
                                normalizedPath,
                                StringComparison
                                    .OrdinalIgnoreCase
                            )
                    );

            /*
             * 只能下载 manifest 中明确列出的文件。
             *
             * 即使攻击者手工构造：
             *
             * ../../appsettings.json
             *
             * 也不会被允许。
             */
            if (manifestFile == null)
            {
                return NotFound(
                    "更新文件不存在"
                );
            }

            string fullPath;

            try
            {
                fullPath =
                    packageStore
                        .GetPublishedFilePath(
                            versionId,
                            manifestFile.Path
                        );
            }
            catch (
                InvalidDataException)
            {
                return BadRequest(
                    "更新文件路径无效"
                );
            }

            if (!System.IO.File.Exists(
                    fullPath))
            {
                return NotFound(
                    "更新文件已丢失，请联系管理员重新上传更新包"
                );
            }

            var stream =
                new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );

            return File(
                stream,
                "application/octet-stream",
                Path.GetFileName(
                    manifestFile.Path
                ),
                enableRangeProcessing:
                    true
            );
        }


        /// <summary>
        /// 完整安装包兜底下载。
        ///
        /// 增量包不存在或客户端增量替换失败时，
        /// Updater 可以下载完整安装程序。
        /// </summary>
        [HttpGet(
            "versions/{versionId:int}/full-package")]
        public async Task<IActionResult>
            DownloadFullPackage(
                int versionId,
                CancellationToken cancellationToken)
        {
            var resolved =
                await ResolveCredentialAsync(
                    softwareCode: null,
                    cancellationToken
                );

            if (resolved == null)
            {
                return Unauthorized();
            }

            var (
                _,
                binding) =
                    resolved.Value;

            var version =
                await GetAuthorizedVersionAsync(
                    versionId,
                    binding.CustomerId,
                    binding.SoftwareId,
                    cancellationToken
                );

            if (version == null)
            {
                return Forbid();
            }

            if (
                !TryGetFullPackagePath(
                    version,
                    out var fullPath)
            )
            {
                return NotFound(
                    "完整安装包不存在"
                );
            }

            var downloadName =
                string.IsNullOrWhiteSpace(
                    version.PackageFileName)
                    ? Path.GetFileName(
                        fullPath)
                    : version.PackageFileName;

            var stream =
                new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read
                );

            return File(
                stream,
                "application/octet-stream",
                downloadName,
                enableRangeProcessing:
                    true
            );
        }


        /// <summary>
        /// 根据 UpdateToken 找到真正的 CustomerSoftware。
        ///
        /// softwareCode 为 null：
        /// 下载文件时只依赖 Token 自身的绑定关系。
        ///
        /// softwareCode 有值：
        /// 检查更新时额外确保客户端声明的软件编码
        /// 与 Token 对应的软件一致。
        /// </summary>
        private async Task<
            (
                ClientUpdateCredentialRecord
                    Credential,
                CustomerSoftware
                    Binding
            )?>
            ResolveCredentialAsync(
                string? softwareCode,
                CancellationToken cancellationToken)
        {
            var token =
                Request.Headers[
                    UpdateTokenHeader
                ]
                .FirstOrDefault();

            if (
                string.IsNullOrWhiteSpace(
                    token)
            )
            {
                return null;
            }

            var tokenHash =
                ClientUpdateTokenService
                    .HashToken(
                        token.Trim()
                    );

            var credentialStore =
                new ClientUpdateCredentialStore(
                    _dbContext
                );

            var credential =
                await credentialStore
                    .GetByTokenHashAsync(
                        tokenHash,
                        cancellationToken
                    );

            if (
                credential == null
                ||
                !credential.IsEnabled
            )
            {
                return null;
            }

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
                            credential
                                .CustomerSoftwareId,
                        cancellationToken
                    );

            if (
                binding == null
                ||
                !binding.IsEnabled
                ||
                binding.Customer == null
                ||
                !binding.Customer.IsEnabled
                ||
                binding.Software == null
                ||
                !binding.Software.IsEnabled
                ||
                !binding.Software.AllowDownload
            )
            {
                return null;
            }

            if (
                !string.IsNullOrWhiteSpace(
                    softwareCode)
                &&
                !string.Equals(
                    binding.Software.Code,
                    softwareCode.Trim(),
                    StringComparison
                        .OrdinalIgnoreCase
                )
            )
            {
                return null;
            }

            return (
                credential,
                binding
            );
        }


        private async Task<
            SoftwareVersion?>
            GetAuthorizedVersionAsync(
                int versionId,
                int customerId,
                int softwareId,
                CancellationToken cancellationToken)
        {
            var audienceExists =
                await _dbContext
                    .SoftwareVersionCustomers
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.SoftwareVersionId
                            ==
                            versionId
                            &&
                            x.CustomerId
                            ==
                            customerId,
                        cancellationToken
                    );

            if (!audienceExists)
            {
                return null;
            }

            return await _dbContext
                .SoftwareVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == versionId
                        &&
                        x.SoftwareId
                        ==
                        softwareId
                        &&
                        x.PublishStatus
                        ==
                        "Published"
                        &&
                        x.IsPublished
                        &&
                        x.AllowDownload,
                    cancellationToken
                );
        }


        private bool TryGetFullPackagePath(
            SoftwareVersion version,
            out string fullPath)
        {
            fullPath =
                string.Empty;

            if (
                string.IsNullOrWhiteSpace(
                    version.PackageRelativePath)
            )
            {
                return false;
            }

            fullPath =
                Path.Combine(
                    _environment.ContentRootPath,
                    version
                        .PackageRelativePath
                        .Replace(
                            '/',
                            Path.DirectorySeparatorChar
                        )
                );

            return System.IO.File.Exists(
                fullPath
            );
        }


        private async Task TouchCredentialAsync(
            ClientUpdateCredentialRecord credential,
            CancellationToken cancellationToken)
        {
            /*
             * LastUsedAt 只是运维信息，
             * 即使更新失败也不能影响主响应。
             */
            try
            {
                var store =
                    new ClientUpdateCredentialStore(
                        _dbContext
                    );

                await store
                    .TouchLastUsedAsync(
                        credential.Id,
                        cancellationToken
                    );
            }
            catch
            {
                // 忽略运维字段更新失败。
            }
        }
    }
}
