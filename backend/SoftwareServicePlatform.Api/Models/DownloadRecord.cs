namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 软件安装包下载记录。
    ///
    /// 每一条记录代表一次真正开始的软件安装包下载。
    ///
    /// 后续可以用于：
    ///
    /// 1. 查询某客户是否下载过某个版本
    /// 2. 查询某版本下载次数
    /// 3. 查询客户下载排行
    /// 4. Dashboard 下载趋势统计
    /// </summary>
    public class DownloadRecord
    {
        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }


        // =====================================================
        // 下载用户
        // =====================================================

        /// <summary>
        /// 谁进行了下载。
        ///
        /// 使用可空外键：
        /// 即使以后用户被删除，
        /// 下载历史仍然可以保留。
        /// </summary>
        public int? UserId { get; set; }


        public User? User { get; set; }


        /// <summary>
        /// 下载时的用户名快照。
        ///
        /// 即使以后用户名称修改，
        /// 历史记录仍然保持当时的信息。
        /// </summary>
        public string UserName { get; set; } = string.Empty;


        /// <summary>
        /// 下载时用户显示名称。
        /// </summary>
        public string UserDisplayName { get; set; } = string.Empty;


        // =====================================================
        // 客户
        // =====================================================

        public int? CustomerId { get; set; }


        public Customer? Customer { get; set; }


        /// <summary>
        /// 下载时客户名称快照。
        /// </summary>
        public string CustomerName { get; set; } = string.Empty;


        // =====================================================
        // 软件
        // =====================================================

        public int? SoftwareId { get; set; }


        public Software? Software { get; set; }


        /// <summary>
        /// 下载时软件名称快照。
        /// </summary>
        public string SoftwareName { get; set; } = string.Empty;


        // =====================================================
        // 软件版本
        // =====================================================

        public int? SoftwareVersionId { get; set; }


        public SoftwareVersion? SoftwareVersion { get; set; }


        /// <summary>
        /// 下载时的软件版本号。
        ///
        /// 例如：
        ///
        /// 1.2.3
        /// </summary>
        public string Version { get; set; } = string.Empty;


        // =====================================================
        // 文件
        // =====================================================

        /// <summary>
        /// 客户下载的安装包文件名。
        /// </summary>
        public string FileName { get; set; } = string.Empty;


        /// <summary>
        /// 安装包文件大小。
        /// </summary>
        public long FileSize { get; set; }


        // =====================================================
        // 时间
        // =====================================================

        /// <summary>
        /// 下载发生时间。
        /// </summary>
        public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    }
}