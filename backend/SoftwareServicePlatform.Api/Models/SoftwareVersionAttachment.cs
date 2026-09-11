namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 软件版本附加资料。
    ///
    /// 一个软件版本除了安装包以外，
    /// 还可以附带：
    ///
    /// 用户手册
    /// 版本说明
    /// 常见问题
    /// 问题日志
    /// 配置文件
    /// 其他资料
    ///
    /// 注意：
    /// 安装包仍然使用 SoftwareVersion
    /// 原来的 PackageXXX 字段。
    ///
    /// 这个表只管理版本的“附加资料”。
    /// </summary>
    public class SoftwareVersionAttachment
    {
        /// <summary>
        /// 附件记录ID。
        /// </summary>
        public int Id { get; set; }


        /// <summary>
        /// 所属的软件版本ID。
        /// </summary>
        public int SoftwareVersionId { get; set; }


        /// <summary>
        /// 所属的软件版本。
        ///
        /// EF Core 导航属性。
        /// </summary>
        public SoftwareVersion SoftwareVersion { get; set; } = null!;


        /// <summary>
        /// 用户上传时的原始文件名。
        ///
        /// 例如：
        /// 路面检测软件用户手册.pdf
        /// </summary>
        public string FileName { get; set; } = string.Empty;


        /// <summary>
        /// 服务器实际保存的文件名。
        ///
        /// 后面上传文件时，
        /// 我们会使用 Guid 生成文件名，
        /// 防止两个用户上传同名文件造成覆盖。
        /// </summary>
        public string StoredFileName { get; set; } = string.Empty;


        /// <summary>
        /// 文件在服务器上的相对路径。
        ///
        /// 这里不保存绝对路径。
        ///
        /// 例如：
        /// Storage/VersionAttachments/2026/09/15/xxxx.pdf
        /// </summary>
        public string StoragePath { get; set; } = string.Empty;


        /// <summary>
        /// 文件大小。
        ///
        /// 单位：字节。
        /// </summary>
        public long FileSize { get; set; }


        /// <summary>
        /// 文件 MIME 类型。
        ///
        /// 例如：
        ///
        /// application/pdf
        /// application/zip
        /// text/plain
        /// </summary>
        public string ContentType { get; set; } = string.Empty;


        /// <summary>
        /// 附件类型。
        ///
        /// 当前约定：
        ///
        /// Manual
        ///     用户手册
        ///
        /// ReleaseDocument
        ///     版本说明
        ///
        /// Troubleshooting
        ///     常见问题 / 问题处理文档
        ///
        /// Log
        ///     问题日志
        ///
        /// Config
        ///     配置文件
        ///
        /// Other
        ///     其他资料
        ///
        /// 暂时使用 string，
        /// 后面增加类型时不用修改数据库结构。
        /// </summary>
        public string AttachmentType { get; set; } = "Other";


        /// <summary>
        /// 客户是否可以看到这个附件。
        ///
        /// true：
        /// Customer 可以在“我的软件”中查看、下载。
        ///
        /// false：
        /// 仅公司内部人员可以看到。
        ///
        /// 例如：
        ///
        /// 用户手册
        ///     true
        ///
        /// 客户现场问题日志
        ///     false
        /// </summary>
        public bool IsCustomerVisible { get; set; } = true;


        /// <summary>
        /// 上传这个附件的用户ID。
        /// </summary>
        public int UploadedByUserId { get; set; }


        /// <summary>
        /// 上传用户。
        ///
        /// EF Core 导航属性。
        /// </summary>
        public User UploadedByUser { get; set; } = null!;


        /// <summary>
        /// 附件备注。
        ///
        /// 例如：
        ///
        /// “适用于二维道路检测软件”
        ///
        /// “2026年9月客户现场问题记录”
        /// </summary>
        public string Remark { get; set; } = string.Empty;


        /// <summary>
        /// 附件上传时间。
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}