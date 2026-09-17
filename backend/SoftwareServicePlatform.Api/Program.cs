using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Hubs;
using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 2L * 1024 * 1024 * 1024;
});

builder.Services.AddControllers();
builder.Services.AddMemoryCache();

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
/*
 * 站内通知服务。
 */
builder.Services.AddScoped< INotificationService, NotificationService>();

builder.Services.AddHostedService<
    SlaNotificationBackgroundService>();

builder.Services.AddSignalR();
/*
 
 * 客户门户工单入口保护：
 * 即使客户手工构造 HTTP 请求传 Urgent，也会在 EF 保存前强制进入待分诊。
 */
builder.Services.AddSingleton<TicketIntakeSaveChangesInterceptor>();

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException("缺少 Jwt:Issuer 配置");
}

if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException("缺少 Jwt:Audience 配置");
}

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("缺少 Jwt:Key 配置");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken =
                    context.Request.Query["access_token"];

                var path =
                    context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken)
                    &&
                    path.StartsWithSegments(
                        "/hubs/notifications"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(serviceProvider.GetRequiredService<TicketIntakeSaveChangesInterceptor>());
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 当前开发环境沿用 HTTP 代理，暂不强制 HTTPS Redirect。
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>(
    "/hubs/notifications");
app.Run();
