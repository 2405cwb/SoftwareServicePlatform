using System.Text.Json.Serialization;

namespace SoftwareServicePlatform.Updater
{
    public sealed class UpdaterConfig
    {
        /// <summary>
        /// 软件服务平台 API 根地址。
        ///
        /// 例如：
        /// https://service.company.com
        /// </summary>
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// 软件平台中的唯一 Software.Code。
        /// </summary>
        public string SoftwareCode { get; set; } = string.Empty;

        /// <summary>
        /// 管理后台生成的客户端更新 Token。
        ///
        /// 它只允许访问当前客户 + 当前软件的更新接口。
        /// </summary>
        public string UpdateToken { get; set; } = string.Empty;

        /// <summary>
        /// 主程序版本文件，相对于 AppRoot。
        /// </summary>
        public string VersionFile { get; set; } = "version.txt";

        /// <summary>
        /// 更新完成后需要重新启动的主程序，相对于 AppRoot。
        /// </summary>
        public string MainExecutable { get; set; } = string.Empty;

        /// <summary>
        /// 增量更新失败时，是否自动回退到完整安装包。
        /// </summary>
        public bool FallbackToFullInstaller { get; set; } = true;

        /// <summary>
        /// 更新成功后是否自动重新启动主程序。
        /// </summary>
        public bool RestartAfterUpdate { get; set; } = true;

        /// <summary>
        /// 等待主程序退出的最大秒数。
        /// </summary>
        public int WaitForProcessSeconds { get; set; } = 60;
    }


    public sealed class ClientUpdateCheckRequest
    {
        public string SoftwareCode { get; set; } = string.Empty;

        public string CurrentVersion { get; set; } = string.Empty;
    }


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

        public ClientUpdateManifest? Manifest { get; set; }
    }


    public sealed class ClientUpdateManifest
    {
        public int SchemaVersion { get; set; }

        public int SoftwareId { get; set; }

        public int VersionId { get; set; }

        public string Version { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; }

        public long TotalFileSize { get; set; }

        public List<ClientUpdateManifestFile> Files { get; set; } = new();

        public List<string> DeletePaths { get; set; } = new();
    }


    public sealed class ClientUpdateManifestFile
    {
        public string Path { get; set; } = string.Empty;

        public long Size { get; set; }

        public string Sha256 { get; set; } = string.Empty;
    }

    public sealed class ClientUpdateReportStartRequest
    {
        public int VersionId { get; set; }

        public string FromVersion { get; set; } =
            string.Empty;

        public string DownloadType { get; set; } =
            string.Empty;

        public int FileCount { get; set; }

        public long FileSize { get; set; }
    }


    public sealed class ClientUpdateReportStartResponse
    {
        public int RecordId { get; set; }
    }


    public sealed class ClientUpdateReportCompleteRequest
    {
        public int RecordId { get; set; }

        public bool Success { get; set; }

        public string ErrorMessage { get; set; } =
            string.Empty;
    }
}
