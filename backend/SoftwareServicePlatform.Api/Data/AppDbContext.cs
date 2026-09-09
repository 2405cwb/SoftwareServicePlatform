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
        }
        public DbSet<Models.Customer> Customers { get; set; } = null!;
        public DbSet<Software> Softwares { get; set; }

        public DbSet<SoftwareVersion> SoftwareVersions { get; set; }

        public DbSet<CustomerSoftware> CustomerSoftwares { get; set; }
    }
}
