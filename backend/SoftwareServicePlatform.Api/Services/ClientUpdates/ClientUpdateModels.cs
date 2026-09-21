using System.Text.Json.Serialization;

namespace SoftwareServicePlatform.Api.Services.ClientUpdates
{
    /// <summary>
    /// 客户端更新包清单。
    ///
    /// 一个版本的增量更新包本质上不是“某个旧版本到新版本的二进制补丁”，
    /// 而是“目标版本完整文件状态”的描述。
    ///
    /// 客户端只要把本地文件 SHA256 与本清单比较，
    /// 就可以只下载发生变化的文件，因此能够跨多个旧版本直接升级到目标版本。
    /// </summary>
    public sealed class ClientUpdateManifest
    {
        public int SchemaVersion { get; set; } = 1;

        public int SoftwareId { get; set; }

        public int VersionId { get; set; }

        public string Version { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        public long TotalFileSize { get; set; }

        public List<ClientUpdateManifestFile> Files { get; set; } = new();

        /// <summary>
        /// 新版本中已经不存在、升级时需要从客户端删除的旧文件。
        /// 路径全部使用 / 作为目录分隔符。
        /// </summary>
        public List<string> DeletePaths { get; set; } = new();
    }


    /// <summary>
    /// 更新清单中的一个目标文件。
    /// </summary>
    public sealed class ClientUpdateManifestFile
    {
        public string Path { get; set; } = string.Empty;

        public long Size { get; set; }

        public string Sha256 { get; set; } = string.Empty;
    }


    /// <summary>
    /// 管理后台展示的更新包摘要。
    /// </summary>
    public sealed class ClientUpdatePackageSummary
    {
        public int VersionId { get; set; }

        public int SoftwareId { get; set; }

        public string SoftwareName { get; set; } = string.Empty;

        public string SoftwareCode { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string VersionType { get; set; } = string.Empty;

        public string PublishStatus { get; set; } = string.Empty;

        public bool HasUpdatePackage { get; set; }

        public int FileCount { get; set; }

        public long TotalFileSize { get; set; }

        public int DeleteCount { get; set; }

        public DateTime? GeneratedAt { get; set; }

        public bool FullPackageAvailable { get; set; }

        public string FullPackageFileName { get; set; } = string.Empty;

        public long FullPackageFileSize { get; set; }
    }


    /// <summary>
    /// 客户端检查更新请求。
    /// UpdateToken 不放在 JSON 中，而是放在 X-Update-Token Header 中，
    /// 避免它出现在常规请求日志的 URL / QueryString 里。
    /// </summary>
    public sealed class ClientUpdateCheckRequest
    {
        public string SoftwareCode { get; set; } = string.Empty;

        public string CurrentVersion { get; set; } = string.Empty;
    }


    /// <summary>
    /// 客户端检查更新返回值。
    /// </summary>
    public sealed class ClientUpdateCheckResponse
    {
        public bool HasUpdate { get; set; }

        public string CurrentVersion { get; set; } = string.Empty;

        public int? VersionId { get; set; }

        public string LatestVersion { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string ReleaseNotes { get; set; } = string.Empty;

        public bool ForceUpdate { get; set; }

        public DateTime? PublishedAt { get; set; }

        public bool IncrementalAvailable { get; set; }

        public long IncrementalTotalSize { get; set; }

        public bool FullPackageAvailable { get; set; }

        public string FullPackageFileName { get; set; } = string.Empty;

        public long FullPackageFileSize { get; set; }

        public string FullPackageSha256 { get; set; } = string.Empty;

        /// <summary>
        /// 有增量更新包时直接把目标版本清单返回客户端。
        /// 客户端本地比较 SHA256 后，只下载真正变化的文件。
        /// </summary>
        public ClientUpdateManifest? Manifest { get; set; }
    }


    /// <summary>
    /// 数据库存储的客户端更新凭证。
    ///
    /// 注意：
    /// 数据库只存 TokenHash，不存明文 Token。
    /// 明文只会在管理员“生成/重置”时返回一次。
    /// </summary>
    public sealed class ClientUpdateCredentialRecord
    {
        public int Id { get; set; }

        public int CustomerSoftwareId { get; set; }

        public string TokenHash { get; set; } = string.Empty;

        public string TokenPrefix { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? LastUsedAt { get; set; }
    }

    /// <summary>
    /// 自动更新开始上报。
    /// </summary>
    public sealed class ClientUpdateReportStartRequest
    {
        public int VersionId { get; set; }

        public string FromVersion { get; set; } =
            string.Empty;

        /// <summary>
        /// AutoIncremental
        /// AutoFullPackage
        /// </summary>
        public string DownloadType { get; set; } =
            string.Empty;

        public int FileCount { get; set; }

        public long FileSize { get; set; }
    }


    /// <summary>
    /// 自动更新开始上报返回值。
    /// </summary>
    public sealed class ClientUpdateReportStartResponse
    {
        public int RecordId { get; set; }
    }


    /// <summary>
    /// 自动更新结束上报。
    /// </summary>
    public sealed class ClientUpdateReportCompleteRequest
    {
        public int RecordId { get; set; }

        public bool Success { get; set; }

        public string ErrorMessage { get; set; } =
            string.Empty;
    }
}
