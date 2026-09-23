using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Services.ClientUpdates;

namespace SoftwareServicePlatform.Api.Services.Releases
{
    /// <summary>
    /// 软件版本发布前统一检查。
    /// 前端用于展示检查结果，后端过滤器用于阻止绕过前端的错误发布。
    /// </summary>
    public sealed class ReleasePreflightService
    {
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;

        public ReleasePreflightService(
            AppDbContext dbContext,
            IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }

        public async Task<ReleasePreflightResult> CheckAsync(
            int versionId,
            CancellationToken cancellationToken = default)
        {
            var result = new ReleasePreflightResult
            {
                VersionId = versionId
            };

            var version =
                await _dbContext.SoftwareVersions
                    .AsNoTracking()
                    .Include(x => x.Software)
                    .FirstOrDefaultAsync(
                        x => x.Id == versionId,
                        cancellationToken);

            if (version == null)
            {
                result.Errors.Add("软件版本不存在");
                return result;
            }

            result.SoftwareName = version.Software?.Name ?? string.Empty;
            result.SoftwareCode = version.Software?.Code ?? string.Empty;
            result.Version = version.Version;
            result.VersionType = version.VersionType;

            if (version.PublishStatus != "Draft")
            {
                result.Errors.Add("只有 Draft 草稿版本允许发布");
            }

            if (version.VersionType == "Dev")
            {
                result.Errors.Add("Dev 版本仅供内部使用，不能发布给客户");
            }

            if (version.Software == null || !version.Software.IsEnabled)
            {
                result.Errors.Add("所属软件不存在或已经停用");
            }
            else if (!version.Software.AllowDownload)
            {
                result.Errors.Add("所属软件当前禁止客户下载");
            }

            var currentVersion = ParseVersion(version.Version);

            if (currentVersion == null)
            {
                result.Errors.Add(
                    $"版本号“{version.Version}”不是有效的数字版本号，建议使用 1.2.3 或 1.2.3.4");
            }
            else
            {
                var publishedVersionTexts =
                    await _dbContext.SoftwareVersions
                        .AsNoTracking()
                        .Where(
                            x => x.SoftwareId == version.SoftwareId
                                 && x.Id != version.Id
                                 && x.PublishStatus == "Published")
                        .Select(x => x.Version)
                        .ToListAsync(cancellationToken);

                var latest =
                    publishedVersionTexts
                        .Select(x => new
                        {
                            Text = x,
                            Parsed = ParseVersion(x)
                        })
                        .Where(x => x.Parsed != null)
                        .OrderByDescending(x => x.Parsed)
                        .FirstOrDefault();

                if (
                    latest?.Parsed != null
                    && currentVersion.CompareTo(latest.Parsed) <= 0)
                {
                    result.Errors.Add(
                        $"目标版本 {version.Version} 必须高于当前已发布版本 {latest.Text}");
                }
            }

            var hasFullPackage =
                !string.IsNullOrWhiteSpace(version.PackageRelativePath);

            result.HasFullPackage = hasFullPackage;

            if (hasFullPackage)
            {
                var fullPath =
                    Path.Combine(
                        _environment.ContentRootPath,
                        version.PackageRelativePath
                            .Replace(
                                '/',
                                Path.DirectorySeparatorChar));

                if (!File.Exists(fullPath))
                {
                    result.Errors.Add(
                        "数据库记录存在完整安装包，但服务器文件已经丢失，请重新上传");
                }
            }

            var packageStore =
                new ClientUpdatePackageStore(
                    _environment.ContentRootPath);

            var manifest =
                packageStore.LoadManifest(version.Id);

            var manifestValid =
                manifest != null
                && manifest.SoftwareId == version.SoftwareId
                && string.Equals(
                    manifest.Version,
                    version.Version,
                    StringComparison.OrdinalIgnoreCase);

            result.HasUpdatePackage = manifestValid;

            if (manifest != null && !manifestValid)
            {
                result.Errors.Add(
                    "自动更新 manifest 与当前 SoftwareId / Version 不匹配，请重新上传更新 ZIP");
            }

            if (!result.HasFullPackage && !result.HasUpdatePackage)
            {
                result.Errors.Add("请至少上传完整安装包或自动更新 ZIP");
            }

            if (!result.HasFullPackage)
            {
                result.Warnings.Add(
                    "没有完整安装包：增量更新失败时无法回退到完整安装程序");
            }

            if (!result.HasUpdatePackage)
            {
                result.Warnings.Add(
                    "没有自动更新 ZIP：客户端只能通过完整安装包升级");
            }

            if (string.IsNullOrWhiteSpace(version.ReleaseNotes))
            {
                result.Warnings.Add("当前版本没有填写更新说明");
            }

            if (string.IsNullOrWhiteSpace(version.Title))
            {
                result.Warnings.Add("当前版本没有填写版本标题");
            }

            result.AuthorizedCustomerCount =
                await _dbContext.CustomerSoftwares
                    .AsNoTracking()
                    .CountAsync(
                        x => x.SoftwareId == version.SoftwareId
                             && x.IsEnabled
                             && x.Customer != null
                             && x.Customer.IsEnabled,
                        cancellationToken);

            if (result.AuthorizedCustomerCount == 0)
            {
                result.Warnings.Add("当前没有任何启用状态的授权客户");
            }

            if (manifestValid)
            {
                result.ManifestFileCount = manifest!.Files.Count;
                result.ManifestTotalFileSize = manifest.TotalFileSize;
                result.ManifestDeleteCount = manifest.DeletePaths.Count;

                result.ContainsUpdaterSelfUpdate =
                    manifest.Files.Any(
                        x => x.Path.Equals(
                            ".updater-self/SoftwareServicePlatform.Updater.exe",
                            StringComparison.OrdinalIgnoreCase));
            }

            return result;
        }

        private static Version? ParseVersion(string text)
        {
            text = (text ?? string.Empty).Trim();

            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                text = text[1..];
            }

            return Version.TryParse(text, out var value)
                ? value
                : null;
        }
    }

    public sealed class ReleasePreflightResult
    {
        public int VersionId { get; set; }
        public string SoftwareName { get; set; } = string.Empty;
        public string SoftwareCode { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string VersionType { get; set; } = string.Empty;

        public bool HasFullPackage { get; set; }
        public bool HasUpdatePackage { get; set; }

        public int ManifestFileCount { get; set; }
        public long ManifestTotalFileSize { get; set; }
        public int ManifestDeleteCount { get; set; }
        public bool ContainsUpdaterSelfUpdate { get; set; }

        public int AuthorizedCustomerCount { get; set; }

        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public bool CanPublish => Errors.Count == 0;
    }
}
