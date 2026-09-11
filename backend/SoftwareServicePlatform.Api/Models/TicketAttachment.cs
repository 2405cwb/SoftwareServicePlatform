namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 工单附件。
    ///
    /// 可以用于：
    ///
    /// 1. 客户首次提交工单时上传附件
    /// 2. 客户后续补充问题时上传附件
    /// 3. 售后回复时上传附件
    /// 4. 开发人员上传日志、截图、分析文件
    /// 5. 内部附件，不允许客户查看
    /// </summary>
    public class TicketAttachment
    {
        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }


        /// <summary>
        /// 所属工单ID。
        ///
        /// 所有附件都必须属于某个 Ticket。
        /// </summary>
        public int TicketId { get; set; }


        /// <summary>
        /// 所属工单。
        /// </summary>
        public Ticket Ticket { get; set; } = null!;


        /// <summary>
        /// 所属处理记录ID。
        ///
        /// 可以为空。
        ///
        /// null：
        /// 代表附件直接属于整个工单，
        /// 例如客户首次提交工单时上传的截图。
        ///
        /// 有值：
        /// 代表附件属于某条 TicketRecord，
        /// 例如开发回复时上传的日志文件。
        /// </summary>
        public int? TicketRecordId { get; set; }


        /// <summary>
        /// 所属处理记录。
        /// </summary>
        public TicketRecord? TicketRecord { get; set; }


        /// <summary>
        /// 上传附件的用户ID。
        /// </summary>
        public int UploadedByUserId { get; set; }


        /// <summary>
        /// 上传附件的用户。
        /// </summary>
        public User UploadedByUser { get; set; } = null!;


        /// <summary>
        /// 用户原始文件名。
        ///
        /// 例如：
        ///
        /// 软件报错截图.png
        /// error-log.zip
        /// </summary>
        public string FileName { get; set; } = string.Empty;


        /// <summary>
        /// 服务器实际保存使用的文件名。
        ///
        /// 不直接使用用户文件名保存，
        /// 避免：
        ///
        /// 文件重名
        /// 路径问题
        /// 特殊字符问题
        ///
        /// 后面我们会使用 Guid 生成。
        /// </summary>
        public string StoredFileName { get; set; } = string.Empty;


        /// <summary>
        /// 文件在服务器上的相对存储路径。
        ///
        /// 注意：
        ///
        /// 数据库不要保存类似：
        ///
        /// D:\xxx\xxx
        ///
        /// 这种绝对路径。
        ///
        /// 后面保存类似：
        ///
        /// tickets/2026/09/123/xxx.png
        /// </summary>
        public string StoragePath { get; set; } = string.Empty;


        /// <summary>
        /// 文件大小，单位：字节。
        /// </summary>
        public long FileSize { get; set; }


        /// <summary>
        /// MIME 类型。
        ///
        /// 例如：
        ///
        /// image/png
        /// application/zip
        /// text/plain
        /// </summary>
        public string ContentType { get; set; } = string.Empty;


        /// <summary>
        /// 是否为内部附件。
        ///
        /// false：
        /// 客户可以查看。
        ///
        /// true：
        /// 只有内部人员可以查看，
        /// Customer 无权获取。
        /// </summary>
        public bool IsInternal { get; set; } = false;


        /// <summary>
        /// 上传时间。
        /// </summary>
        public DateTime CreatedAt { get; set; } =
            DateTime.UtcNow;
    }
}