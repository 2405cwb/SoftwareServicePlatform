using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Data
{
    public class AppDbContext:DbContext
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
        }
        public DbSet<Models.Customer> Customers { get; set; } = null!;
        public DbSet<Software> Softwares { get; set; }

        public DbSet<SoftwareVersion> SoftwareVersions { get; set; }
    }
}
