using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Software>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<SoftwareVersion>()
                .HasOne(x => x.Software)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.SoftwareId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasOne(x => x.Customer)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CustomerSoftware>()
                .HasOne(x => x.Customer)
                .WithMany(x => x.CustomerSoftwares)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerSoftware>()
                .HasOne(x => x.Software)
                .WithMany(x => x.CustomerSoftwares)
                .HasForeignKey(x => x.SoftwareId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CustomerSoftware>()
                .HasIndex(x => new
                {
                    x.CustomerId,
                    x.SoftwareId
                })
                .IsUnique();

            modelBuilder.Entity<CustomerSoftware>()
                .Property(x => x.MaxDeviceCount)
                .HasDefaultValue(0);

            // 设备级更新授权
            modelBuilder.Entity<ClientInstallation>()
                .HasOne(x => x.CustomerSoftware)
                .WithMany(x => x.ClientInstallations)
                .HasForeignKey(x => x.CustomerSoftwareId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ClientActivationCode>()
                .HasOne(x => x.CustomerSoftware)
                .WithMany(x => x.ClientActivationCodes)
                .HasForeignKey(x => x.CustomerSoftwareId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ticket
            modelBuilder.Entity<Ticket>()
                .HasIndex(x => x.TicketNo)
                .IsUnique();

            modelBuilder.Entity<Ticket>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .HasOne(x => x.Software)
                .WithMany()
                .HasForeignKey(x => x.SoftwareId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .HasOne(x => x.AssignedToUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Ticket>()
                .Property(x => x.Source)
                .HasMaxLength(30)
                .HasDefaultValue("Portal");

            modelBuilder.Entity<Ticket>()
                .Property(x => x.SlaPriority)
                .HasMaxLength(30);

            modelBuilder.Entity<TicketRecord>()
                .HasOne(x => x.Ticket)
                .WithMany(x => x.Records)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TicketRecord>()
                .HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketAttachment>()
                .HasOne(x => x.Ticket)
                .WithMany()
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TicketAttachment>()
                .HasOne(x => x.TicketRecord)
                .WithMany()
                .HasForeignKey(x => x.TicketRecordId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TicketAttachment>()
                .HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 软件版本资料
            modelBuilder.Entity<SoftwareVersionAttachment>()
                .HasOne(x => x.SoftwareVersion)
                .WithMany()
                .HasForeignKey(x => x.SoftwareVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SoftwareVersionAttachment>()
                .HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 下载 / 更新记录
            modelBuilder.Entity<DownloadRecord>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<DownloadRecord>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<DownloadRecord>()
                .HasOne(x => x.Software)
                .WithMany()
                .HasForeignKey(x => x.SoftwareId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<DownloadRecord>()
                .HasOne(x => x.SoftwareVersion)
                .WithMany()
                .HasForeignKey(x => x.SoftwareVersionId)
                .OnDelete(DeleteBehavior.SetNull);

            // SLA
            modelBuilder.Entity<TicketSlaRule>()
                .Property(x => x.Priority)
                .HasMaxLength(30);

            modelBuilder.Entity<TicketSlaRule>()
                .HasIndex(x => x.Priority)
                .IsUnique();

            // Notification
            modelBuilder.Entity<Notification>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .Property(x => x.Type)
                .HasMaxLength(50);

            modelBuilder.Entity<Notification>()
                .Property(x => x.Level)
                .HasMaxLength(20);

            modelBuilder.Entity<Notification>()
                .Property(x => x.Title)
                .HasMaxLength(200);

            modelBuilder.Entity<Notification>()
                .Property(x => x.TargetUrl)
                .HasMaxLength(500);

            modelBuilder.Entity<Notification>()
                .Property(x => x.DedupKey)
                .HasMaxLength(200);

            modelBuilder.Entity<Notification>()
                .HasIndex(x => new
                {
                    x.UserId,
                    x.DedupKey
                })
                .IsUnique();

            modelBuilder.Entity<Notification>()
                .HasIndex(x => new
                {
                    x.UserId,
                    x.IsRead,
                    x.CreatedAt
                });

            // 版本发布范围
            modelBuilder.Entity<SoftwareVersionCustomer>()
                .HasOne(x => x.SoftwareVersion)
                .WithMany()
                .HasForeignKey(x => x.SoftwareVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SoftwareVersionCustomer>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SoftwareVersionCustomer>()
                .HasIndex(x => new
                {
                    x.SoftwareVersionId,
                    x.CustomerId
                })
                .IsUnique();

            // 外部通知用户绑定
            modelBuilder.Entity<ExternalUserBinding>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExternalUserBinding>()
                .HasIndex(x => new
                {
                    x.UserId,
                    x.Channel
                })
                .IsUnique();

            modelBuilder.Entity<ExternalUserBinding>()
                .Property(x => x.Channel)
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<ExternalUserBinding>()
                .Property(x => x.ExternalUserId)
                .HasMaxLength(200);

            modelBuilder.Entity<ExternalUserBinding>()
                .Property(x => x.Mobile)
                .HasMaxLength(50);

            // 通知策略
            modelBuilder.Entity<NotificationPolicy>()
                .HasIndex(x => x.EventKey)
                .IsUnique();

            modelBuilder.Entity<NotificationPolicy>()
                .Property(x => x.EventKey)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<NotificationPolicy>()
                .Property(x => x.EventName)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<NotificationPolicy>()
                .Property(x => x.Description)
                .HasMaxLength(500);

            modelBuilder.Entity<NotificationPolicy>()
                .Property(x => x.RecipientStrategy)
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<NotificationPolicy>()
                .Property(x => x.DefaultLevel)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<NotificationPolicyChannel>()
                .HasOne(x => x.NotificationPolicy)
                .WithMany(x => x.Channels)
                .HasForeignKey(x => x.NotificationPolicyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NotificationPolicyChannel>()
                .HasIndex(x => new
                {
                    x.NotificationPolicyId,
                    x.Channel
                })
                .IsUnique();

            modelBuilder.Entity<NotificationPolicyChannel>()
                .Property(x => x.Channel)
                .HasMaxLength(50)
                .IsRequired();

            /*
             * 旧 ClientUpdateCredential 已从 EF 模型移除。
             * 新 Migration 会删除旧共享 Token 表。
             */
        }

        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Software> Softwares { get; set; } = null!;
        public DbSet<SoftwareVersion> SoftwareVersions { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<CustomerSoftware> CustomerSoftwares { get; set; } = null!;
        public DbSet<ClientInstallation> ClientInstallations { get; set; } = null!;
        public DbSet<ClientActivationCode> ClientActivationCodes { get; set; } = null!;
        public DbSet<Ticket> Tickets { get; set; } = null!;
        public DbSet<TicketRecord> TicketRecords { get; set; } = null!;
        public DbSet<TicketAttachment> TicketAttachments { get; set; } = null!;
        public DbSet<SoftwareVersionAttachment> SoftwareVersionAttachments { get; set; } = null!;
        public DbSet<DownloadRecord> DownloadRecords { get; set; } = null!;
        public DbSet<TicketSlaRule> TicketSlaRules { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<SoftwareVersionCustomer> SoftwareVersionCustomers { get; set; } = null!;
        public DbSet<ExternalUserBinding> ExternalUserBindings { get; set; } = null!;
        public DbSet<NotificationPolicy> NotificationPolicies { get; set; } = null!;
        public DbSet<NotificationPolicyChannel> NotificationPolicyChannels { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<SystemEventLog> SystemEventLogs { get; set; } = null!;
    }
}
