namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 软件版本信息
    /// </summary>
    public class SoftwareVersion
    {
        /// <summary>
        /// 版本记录ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 所属软件ID
        /// </summary>
        public int SoftwareId { get; set; }

        /// <summary>
        /// 版本号
        /// 例如：1.0.0、2.1.3
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 版本类型
        /// 例如：Release、Beta、Dev
        /// </summary>
        public string VersionType { get; set; } = "Release";

        /// <summary>
        /// 版本标题
        /// 例如：2026年9月正式版本
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 本次版本更新说明
        /// </summary>
        public string ReleaseNotes { get; set; } = string.Empty;

        /// <summary>
        /// 是否已正式发布
        /// </summary>
        public bool IsPublished { get; set; } = false;

        /// <summary>
        /// 是否强制升级
        /// </summary>
        public bool ForceUpdate { get; set; } = false;

        /// <summary>
        /// 是否允许下载
        /// </summary>
        public bool AllowDownload { get; set; } = true;

        /// <summary>
        /// 发布时间
        ///
        /// null 表示目前尚未正式发布
        /// </summary>
        public DateTime? PublishedAt { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 所属的软件
        ///
        /// 这是 EF Core 的导航属性。
        /// 一个 SoftwareVersion 只属于一个 Software。
        /// </summary>
        public Software? Software { get; set; }


        /// <summary>
        /// 安装包原始文件名
        /// 例如：RoadProcess_Setup_1.0.0.exe
        /// </summary>
        public string PackageFileName { get; set; } = string.Empty;

        /// <summary>
        /// 安装包文件大小，单位：字节
        /// </summary>
        public long PackageFileSize { get; set; }

        /// <summary>
        /// 安装包在服务器上的相对路径
        /// </summary>
        public string PackageRelativePath { get; set; } = string.Empty;

        /// <summary>
        /// 安装包 SHA256 校验值
        /// </summary>
        public string PackageSha256 { get; set; } = string.Empty;

        /// <summary>
        /// 安装包上传时间
        /// </summary>
        public DateTime? PackageUploadedAt { get; set; }
    }
}