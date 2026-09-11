using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Software>().HasIndex(x => x.Code).IsUnique();

            modelBuilder.Entity<SoftwareVersion>()
    .HasOne(x => x.Software)
    .WithMany(x => x.Versions)
    .HasForeignKey(x => x.SoftwareId)
    .OnDelete(DeleteBehavior.Cascade);


            /*
 * 登录账号必须唯一。
 *
 * 不能出现两个：
 *
 * Username = admin
 */
            modelBuilder.Entity<User>()
                .HasIndex(x => x.Username)
                .IsUnique();


            /*
 * Customer
 *    1
 *    ↓
 *    N 
 * CustomerSoftware
 */
            modelBuilder.Entity<CustomerSoftware>()
                .HasOne(x => x.Customer)
                .WithMany(x => x.CustomerSoftwares)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            /*
 * Software
 *    1
 *    ↓
 *    N
 * CustomerSoftware
 */
            modelBuilder.Entity<CustomerSoftware>()
                .HasOne(x => x.Software)
                .WithMany(x => x.CustomerSoftwares)
                .HasForeignKey(x => x.SoftwareId)
                .OnDelete(DeleteBehavior.Cascade);


            /*
 * 同一个客户不能重复绑定同一个软件。
 *
 * CustomerId + SoftwareId
 * 这个组合必须唯一。
 */
            modelBuilder.Entity<CustomerSoftware>()
                .HasIndex(x => new
                {
                    x.CustomerId,
                    x.SoftwareId
                })
                .IsUnique();

            /* User
            *
            * 一个客户可以有多个登录用户。
            *
            * 一个 User 最多属于一个 Customer。
            */
            modelBuilder.Entity<User>()
                .HasOne(x => x.Customer)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
            /*
            * ==========================================
            * Ticket 工单配置
            * ==========================================
            */


            /*
             * 工单编号必须唯一。
             *
             * 不允许出现两个：
             *
             * TK202609090001
             */
            modelBuilder.Entity<Ticket>()
                .HasIndex(x => x.TicketNo)
                .IsUnique();


            /*
 * 一个 Customer 可以拥有多个 Ticket。
 *
 * 当前阶段不在 Customer 中增加 Tickets 集合，
 * 所以这里直接使用 WithMany()。
 */
            modelBuilder.Entity<Ticket>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);


            /*
 * 一个 Software 可以对应多个 Ticket。
 *
 * 软件一旦有历史工单，
 * 不允许因为删除 Software
 * 而把工单历史一起删除。
 */
            modelBuilder.Entity<Ticket>()
                .HasOne(x => x.Software)
                .WithMany()
                .HasForeignKey(x => x.SoftwareId)
                .OnDelete(DeleteBehavior.Restrict);


            /*
 * 工单创建人。
 *
 * 一个 User 可以创建很多工单。
 */
            modelBuilder.Entity<Ticket>()
                .HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);


            /*
 * 当前处理人。
 *
 * AssignedToUserId 可以为空，
 * 因为刚创建的工单可能尚未分配处理人。
 *
 * 如果以后处理人账号被真正删除，
 * 可以把 AssignedToUserId 自动置空。
 */
            modelBuilder.Entity<Ticket>()
                .HasOne(x => x.AssignedToUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);


            /*
 * ==========================================
 * TicketRecord 工单处理记录配置
 * ==========================================
 */


            /*
             * Ticket
             *    1
             *    ↓
             *    N
             * TicketRecord
             *
             * 删除 Ticket 时，
             * 它下面的处理记录也一起删除。
             *
             * 为什么这里适合 Cascade：
             *
             * TicketRecord 本身没有脱离 Ticket
             * 独立存在的意义。
             */
            modelBuilder.Entity<TicketRecord>()
                .HasOne(x => x.Ticket)
                .WithMany(x => x.Records)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);


            /*
 * TicketRecord -> CreatedByUser
 *
 * 每一条处理记录都必须知道是谁写的。
 *
 * 不允许因为删除一个用户，
 * 就把历史处理记录全部删除。
 */
            modelBuilder.Entity<TicketRecord>()
                .HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
        public DbSet<Models.Customer> Customers { get; set; } = null!;
        public DbSet<Software> Softwares { get; set; }

        public DbSet<SoftwareVersion> SoftwareVersions { get; set; }


        public DbSet<User> Users { get; set; }


        public DbSet<CustomerSoftware> CustomerSoftwares { get; set; }


        /// <summary>
        /// 工单数据。
        /// </summary>
        public DbSet<Ticket> Tickets { get; set; }

        /// <summary>
        /// 工单处理记录。
        /// </summary>
        public DbSet<TicketRecord> TicketRecords { get; set; }
    }
}
