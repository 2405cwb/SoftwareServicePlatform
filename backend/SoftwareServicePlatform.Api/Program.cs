using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Hubs;
using SoftwareServicePlatform.Api.Middleware;
using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services;
using SoftwareServicePlatform.Api.Services.ExternalNotifications;
using SoftwareServicePlatform.Api.Services.NotificationPolicies;
using SoftwareServicePlatform.Api.Services.Releases;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        2L * 1024 * 1024 * 1024;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize =
        2L * 1024 * 1024 * 1024;
});

builder.Services.AddScoped<ReleasePreflightService>();
builder.Services.AddScoped<PublishPreflightFilter>();

builder.Services.AddControllers(options =>
{
    /*
     * 所有 PublishVersion 请求都会经过统一发布前检查，
     * 防止只靠前端校验被绕过。
     */
    options.Filters.AddService<PublishPreflightFilter>();
});

builder.Services.AddMemoryCache();

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

builder.Services.AddScoped<
    INotificationService,
    NotificationService>();

builder.Services.AddHostedService<
    SlaNotificationBackgroundService>();

builder.Services.AddSignalR();

builder.Services.AddScoped<
    NotificationPolicySeeder>();

builder.Services.AddScoped<
    AdminAccountSeeder>();

builder.Services.AddScoped<
    IExternalNotificationService,
    ExternalNotificationService>();

builder.Services.Configure<DingTalkOptions>(
    builder.Configuration.GetSection(
        "DingTalk"));

builder.Services.AddHttpClient<
    DingTalkNotificationSender>(
    client =>
    {
        client.Timeout =
            TimeSpan.FromSeconds(10);
    });

builder.Services.AddScoped<
    INotificationRecipientResolver,
    NotificationRecipientResolver>();

builder.Services.AddScoped<
    INotificationEventService,
    NotificationEventService>();

builder.Services.AddScoped<
    IExternalNotificationSender>(
        serviceProvider =>
            serviceProvider.GetRequiredService<
                DingTalkNotificationSender>());

builder.Services.AddSingleton<
    TicketIntakeSaveChangesInterceptor>();

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"];

var jwtAudience =
    builder.Configuration["Jwt:Audience"];

var jwtKey =
    builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException(
        "缺少 Jwt:Issuer 配置");
}

if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "缺少 Jwt:Audience 配置");
}

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "缺少 Jwt:Key 配置");
}

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ClockSkew =
                    TimeSpan.FromMinutes(1)
            };

        options.Events =
            new JwtBearerEvents
            {
                OnMessageReceived =
                    context =>
                    {
                        var accessToken =
                            context.Request.Query[
                                "access_token"];

                        var path =
                            context.HttpContext
                                .Request.Path;

                        if (
                            !string.IsNullOrEmpty(
                                accessToken)
                            && path.StartsWithSegments(
                                "/hubs/notifications"))
                        {
                            context.Token =
                                accessToken;
                        }

                        return Task.CompletedTask;
                    }
            };
    });

builder.Services.AddScoped<
    INotificationPolicyService,
    NotificationPolicyService>();

builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>(
    (serviceProvider, options) =>
    {
        options.UseNpgsql(
            builder.Configuration
                .GetConnectionString(
                    "DefaultConnection"));

        options.AddInterceptors(
            serviceProvider
                .GetRequiredService<
                    TicketIntakeSaveChangesInterceptor>());
    });

builder.Services.AddOpenApi();

var app = builder.Build();

/*
 * 自动执行尚未应用的 EF Core Migration。
 */
using (var scope = app.Services.CreateScope())
{
    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    await dbContext.Database.MigrateAsync();
}

/*
 * 初始化默认通知策略和首个管理员。
 */
using (var scope = app.Services.CreateScope())
{
    var notificationPolicySeeder =
        scope.ServiceProvider
            .GetRequiredService<
                NotificationPolicySeeder>();

    await notificationPolicySeeder.SeedAsync();

    var adminAccountSeeder =
        scope.ServiceProvider
            .GetRequiredService<
                AdminAccountSeeder>();

    await adminAccountSeeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 当前阶段继续使用 HTTP。
// HTTPS 留到后续正式工程化部署。
// app.UseHttpsRedirection();

/*
 * SystemEventMiddleware 放在外层，
 * 用于捕获后续管线中的未处理异常和 5xx。
 */
app.UseMiddleware<SystemEventMiddleware>();

app.UseAuthentication();

/*
 * AuditLogMiddleware 必须放在 Authentication 后，
 * 才能获取当前登录用户 Claims。
 */
app.UseMiddleware<AuditLogMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.MapHub<NotificationHub>(
    "/hubs/notifications");

app.Run();
