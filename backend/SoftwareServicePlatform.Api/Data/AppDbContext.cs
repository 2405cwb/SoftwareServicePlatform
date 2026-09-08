using Microsoft.EntityFrameworkCore;

namespace SoftwareServicePlatform.Api.Data
{
    public class AppDbContext:DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Models.Customer> Customers { get; set; } = null!;  
    }
}
