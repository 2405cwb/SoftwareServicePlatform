using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SoftwareServicePlatform.Api.Models
{
    [Index(nameof(InstallationId), IsUnique = true)]
    [Index(nameof(TokenHash), IsUnique = true)]
    [Index(nameof(CustomerSoftwareId))]
    public class ClientInstallation
    {
        public int Id { get; set; }
        public int CustomerSoftwareId { get; set; }
        public CustomerSoftware? CustomerSoftware { get; set; }

        [MaxLength(64)]
        public string InstallationId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string DeviceName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remark { get; set; } = string.Empty;

        [MaxLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        [MaxLength(64)]
        public string TokenPrefix { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;
        public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
