using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 客户端软件安装实例。
    ///
    /// 设计原则：
    /// 1. CustomerSoftware 表示“公司有没有这款软件的授权”；
    /// 2. ClientInstallation 表示“这家公司在哪些设备上安装了这款软件”；
    /// 3. 每个安装实例拥有自己独立的 UpdateToken；
    /// 4. 数据库只保存 TokenHash，不保存明文 Token。
    /// </summary>
    [Index(nameof(InstallationId), IsUnique = true)]
    [Index(nameof(TokenHash), IsUnique = true)]
    [Index(nameof(CustomerSoftwareId))]
    public class ClientInstallation
    {
        public int Id { get; set; }

        /// <summary>
        /// 所属“客户 + 软件”授权关系。
        /// </summary>
        public int CustomerSoftwareId { get; set; }

        public CustomerSoftware? CustomerSoftware { get; set; }

        /// <summary>
        /// 安装实例唯一标识。
        ///
        /// 不绑定 CPU、MAC、硬盘序列号等硬件指纹，
        /// 避免硬件更换、虚拟网卡变化等情况导致误判。
        /// </summary>
        [MaxLength(64)]
        public string InstallationId { get; set; } = string.Empty;

        /// <summary>
        /// 设备显示名称。
        /// 默认由客户端上传 Environment.MachineName。
        /// </summary>
        [MaxLength(100)]
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// 当前设备 UpdateToken 的 SHA256。
        /// </summary>
        [MaxLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>
        /// 后台展示用的 Token 前缀。
        /// 不保存完整明文 Token。
        /// </summary>
        [MaxLength(64)]
        public string TokenPrefix { get; set; } = string.Empty;

        /// <summary>
        /// 当前安装实例是否允许继续检查和下载更新。
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 首次激活时间。
        /// </summary>
        public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 最近一次成功使用 UpdateToken 检查更新的时间。
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// 最后修改时间。
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
