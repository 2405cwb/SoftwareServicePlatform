using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 客户端一次性激活码。
    ///
    /// 客户在网页端生成激活码后，
    /// 在新安装的软件中输入一次。
    /// 服务端验证成功后为该设备创建独立 ClientInstallation，
    /// 并返回该设备自己的 UpdateToken。
    ///
    /// 数据库只保存 CodeHash，不保存激活码明文。
    /// </summary>
    [Index(nameof(CodeHash), IsUnique = true)]
    [Index(nameof(CustomerSoftwareId))]
    public class ClientActivationCode
    {
        public int Id { get; set; }

        /// <summary>
        /// 激活码属于哪一个“客户 + 软件”授权。
        /// </summary>
        public int CustomerSoftwareId { get; set; }

        public CustomerSoftware? CustomerSoftware { get; set; }

        /// <summary>
        /// 激活码标准化后的 SHA256。
        /// </summary>
        [MaxLength(64)]
        public string CodeHash { get; set; } = string.Empty;

        /// <summary>
        /// 创建激活码的网页登录用户。
        /// 这里只保存用户ID用于审计，不作为权限判断依据。
        /// </summary>
        public int? CreatedByUserId { get; set; }

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 到期时间。
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// 实际使用时间。
        /// null 表示尚未使用。
        /// </summary>
        public DateTime? UsedAt { get; set; }

        /// <summary>
        /// 使用该激活码创建出来的安装实例ID。
        /// </summary>
        [MaxLength(64)]
        public string UsedByInstallationId { get; set; } = string.Empty;
    }
}
