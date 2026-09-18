using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Hubs;
using SoftwareServicePlatform.Api.Models;
using SoftwareServicePlatform.Api.Services;
using SoftwareServicePlatform.Api.Services.ExternalNotifications;
using SoftwareServicePlatform.Api.Services.NotificationPolicies;
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
 * ==========================================
 * 通知策略初始化服务
 * ==========================================
 *
 * 系统启动时检查默认通知策略。
 *
 * 只补充缺失策略，
 * 不覆盖管理员已经修改的配置。
 */
builder.Services.AddScoped<
    NotificationPolicySeeder>();
/*
 * ==========================================
 * 外部通知统一调度服务
 * ==========================================
 *
 * Controller 和业务 Service
 * 后面统一依赖这个接口。
 */
builder.Services.AddScoped<
    IExternalNotificationService,
    ExternalNotificationService>();


/*
 * ==========================================
 * 钉钉配置
 * ==========================================
 *
 * 把配置文件：
 *
 * "DingTalk": {
 *     "Enabled": true,
 *     "Webhook": "...",
 *     "Keyword": "软件服务平台"
 * }
 *
 * 绑定到 DingTalkOptions。
 */
builder.Services.Configure<DingTalkOptions>(
    builder.Configuration.GetSection(
        "DingTalk"
    )
);


/*
 * ==========================================
 * 钉钉 HTTP Sender
 * ==========================================
 *
 * 由 IHttpClientFactory 管理 HttpClient。
 */
builder.Services.AddHttpClient<
    DingTalkNotificationSender>(
    client =>
    {
        /*
         * 外部平台出现网络问题时，
         * 不能让请求一直卡着。
         */
        client.Timeout =
            TimeSpan.FromSeconds(10);
    });

/*
 * ==========================================
 * 通知接收人解析器
 * ==========================================
 *
 * 把 NotificationPolicy 中的：
 *
 * RecipientStrategy
 *
 * 转换成真正的系统用户。
 */
builder.Services.AddScoped<
    INotificationRecipientResolver,
    NotificationRecipientResolver>();


/*
 * ==========================================
 * 系统统一通知事件调度器
 * ==========================================
 *
 * 以后 Controller / Service
 * 原则上只和这个接口交互。
 */
builder.Services.AddScoped<
    INotificationEventService,
    NotificationEventService>();
/*
 * ==========================================
 * 把钉钉 Sender 注册成统一 Sender
 * ==========================================
 *
 * ExternalNotificationService 中：
 *
 * IEnumerable<IExternalNotificationSender>
 *
 * 就能找到 DingTalkNotificationSender。
 */
builder.Services.AddScoped<
    IExternalNotificationSender>(
        serviceProvider =>
            serviceProvider.GetRequiredService<
                DingTalkNotificationSender>()
    );

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
/*
 * ==========================================
 * 通知策略读取服务
 * ==========================================
 *
 * 以后所有业务模块读取：
 *
 * NotificationPolicy
 *
 * 都统一经过这个服务。
 */
builder.Services.AddScoped<
    INotificationPolicyService,
    NotificationPolicyService>();
builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(serviceProvider.GetRequiredService<TicketIntakeSaveChangesInterceptor>());
});

builder.Services.AddOpenApi();

var app = builder.Build();

/*
 * ==========================================
 * 初始化通知策略
 * ==========================================
 *
 * 创建一个临时 DI Scope，
 * 因为 NotificationPolicySeeder
 * 依赖 Scoped 的 AppDbContext。
 */
using (var scope =
       app.Services.CreateScope())
{
    var notificationPolicySeeder =
        scope.ServiceProvider
            .GetRequiredService<
                NotificationPolicySeeder>();


    await notificationPolicySeeder
        .SeedAsync();
}


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
