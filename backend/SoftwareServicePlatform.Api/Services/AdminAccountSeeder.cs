using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Services
{
    /// <summary>
    /// 系统管理员初始化服务。
    ///
    /// 主要解决：
    /// 全新数据库第一次启动时 Users 表为空，
    /// 没有管理员可以登录系统的问题。
    ///
    /// 初始化账号通过环境变量 / 配置读取，
    /// 不把管理员密码写死在源码中。
    /// </summary>
    public class AdminAccountSeeder
    {
        private readonly AppDbContext _dbContext;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminAccountSeeder> _logger;

        public AdminAccountSeeder(
            AppDbContext dbContext,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration,
            ILogger<AdminAccountSeeder> logger)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            // 是否启用管理员初始化
            var enabled =
                _configuration.GetValue<bool>(
                    "BootstrapAdmin:Enabled");

            if (!enabled)
            {
                return;
            }

            // 如果数据库已经存在管理员，就不再创建
            var adminExists =
                await _dbContext.Users.AnyAsync(
                    x => x.Role == "Admin");

            if (adminExists)
            {
                _logger.LogInformation(
                    "系统已存在管理员账号，跳过管理员初始化。");

                return;
            }

            var username =
                _configuration["BootstrapAdmin:Username"]
                ?.Trim()
                .ToLowerInvariant();

            var password =
                _configuration["BootstrapAdmin:Password"];

            var displayName =
                _configuration["BootstrapAdmin:DisplayName"]
                ?.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException(
                    "BootstrapAdmin:Username 未配置");
            }

            if (string.IsNullOrWhiteSpace(password)
                || password.Length < 8)
            {
                throw new InvalidOperationException(
                    "BootstrapAdmin:Password 未配置或少于8位");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "系统管理员";
            }

            var admin = new User
            {
                Username = username,
                DisplayName = displayName,

                // 管理员属于公司内部账号，
                // 不绑定 Customer。
                CustomerId = null,

                Role = "Admin",
                IsEnabled = true,

                Email = string.Empty,
                Phone = string.Empty,

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // 使用和登录接口完全相同的 PasswordHasher，
            // 数据库只保存密码 Hash。
            admin.PasswordHash =
                _passwordHasher.HashPassword(
                    admin,
                    password);

            _dbContext.Users.Add(admin);

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "首次管理员账号 {Username} 创建成功。",
                username);
        }
    }
}