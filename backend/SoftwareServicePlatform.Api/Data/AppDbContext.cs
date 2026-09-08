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
        }
        public DbSet<Models.Customer> Customers { get; set; } = null!;
        public DbSet<Software> Softwares { get; set; }
    }
}
