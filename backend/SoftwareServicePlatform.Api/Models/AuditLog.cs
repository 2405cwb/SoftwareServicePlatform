using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SoftwareServicePlatform.Api.Models
{
    /// <summary>
    /// 管理平台操作审计。不保存请求 Body。
    /// </summary>
    [Index(nameof(CreatedAt))]
    [Index(nameof(UserId), nameof(CreatedAt))]
    public class AuditLog
    {
        public long Id { get; set; }
        public int? UserId { get; set; }

        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Role { get; set; } = string.Empty;

        [MaxLength(10)]
        public string HttpMethod { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Path { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ActionName { get; set; } = string.Empty;

        public int StatusCode { get; set; }
        public long DurationMs { get; set; }

        [MaxLength(100)]
        public string IpAddress { get; set; } = string.Empty;

        [MaxLength(500)]
        public string UserAgent { get; set; } = string.Empty;

        [MaxLength(100)]
        public string TraceId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
