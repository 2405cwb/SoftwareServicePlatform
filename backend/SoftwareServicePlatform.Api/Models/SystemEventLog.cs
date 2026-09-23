using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SoftwareServicePlatform.Api.Models
{
    [Index(nameof(CreatedAt))]
    [Index(nameof(Level), nameof(CreatedAt))]
    public class SystemEventLog
    {
        public long Id { get; set; }

        [MaxLength(20)]
        public string Level { get; set; } = "Error";

        [MaxLength(100)]
        public string Source { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        [MaxLength(8000)]
        public string Detail { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Path { get; set; } = string.Empty;

        [MaxLength(10)]
        public string HttpMethod { get; set; } = string.Empty;

        public int StatusCode { get; set; }

        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string IpAddress { get; set; } = string.Empty;

        [MaxLength(100)]
        public string TraceId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
