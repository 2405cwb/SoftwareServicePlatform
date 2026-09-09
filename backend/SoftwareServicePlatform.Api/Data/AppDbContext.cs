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
<<<<<<< Updated upstream
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
 
        }
        public DbSet<Models.Customer> Customers { get; set; } = null!;
        public DbSet<Software> Softwares { get; set; }

        public DbSet<SoftwareVersion> SoftwareVersions { get; set; }

 
        public DbSet<User> Users { get; set; }

 
        public DbSet<CustomerSoftware> CustomerSoftwares { get; set; }
    }
}
