using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using SoftwareServicePlatform.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
var builder = WebApplication.CreateBuilder(args);
/*
 * 软件安装包可能比较大，
 * 所以提高 multipart/form-data 上传大小限制。
 *
 * 当前设置为最大 2GB。
 */
builder.Services.Configure<FormOptions>(
    options =>
    {
        options.MultipartBodyLengthLimit =
            2L * 1024 * 1024 * 1024;
    }
);

/*
 * Kestrel 本身也有请求体大小限制，
 * 同样调整为最大 2GB。
 */
builder.WebHost.ConfigureKestrel(
    options =>
    {
        options.Limits.MaxRequestBodySize =
            2L * 1024 * 1024 * 1024;
    }
);
// Add services to the container.

builder.Services.AddControllers();

/*
 * 注册密码 Hash 服务。
 *
 * IPasswordHasher<User>
 * 表示：
 * 给 User 类型提供密码 Hash / 验证功能。
 */
builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>
>();


/*
 * =========================
 * JWT 身份认证
 * =========================
 */

/*
 * 从 appsettings 中读取 JWT 配置。
 */
var jwtIssuer =
    builder.Configuration["Jwt:Issuer"];

var jwtAudience =
    builder.Configuration["Jwt:Audience"];

var jwtKey =
    builder.Configuration["Jwt:Key"];


/*
 * 配置不存在时直接阻止程序启动。
 *
 * 这样比程序运行以后才莫名其妙报错更容易排查。
 */
if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException(
        "缺少 Jwt:Issuer 配置"
    );
}

if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "缺少 Jwt:Audience 配置"
    );
}

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "缺少 Jwt:Key 配置"
    );
}


/*
 * AddAuthentication：
 *
 * 告诉 ASP.NET Core：
 *
 * 我们以后主要使用 JWT Bearer
 * 来识别用户身份。
 */
builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme
    )
    .AddJwtBearer(options =>
    {
        /*
         * TokenValidationParameters
         *
         * 用来规定：
         * 一个 Token 满足什么条件，
         * 才认为它是真的、有效的。
         */
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                /*
                 * 检查是谁签发的 Token
                 */
                ValidateIssuer = true,

                ValidIssuer = jwtIssuer,

                /*
                 * 检查 Token 是签发给谁使用的
                 */
                ValidateAudience = true,

                ValidAudience = jwtAudience,

                /*
                 * 检查 Token 有没有过期
                 */
                ValidateLifetime = true,

                /*
                 * 检查 Token 签名是否正确
                 */
                ValidateIssuerSigningKey = true,

                /*
                 * 使用我们自己的 Key
                 * 验证 Token 签名。
                 */
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtKey
                        )
                    ),

                /*
                 * Token 过期时间误差。
                 *
                 * 学习阶段设为1分钟。
                 */
                ClockSkew =
                    TimeSpan.FromMinutes(1)
            };
    });


/*
 * 开启授权系统。
 *
 * 后面 [Authorize] 会使用它。
 */
builder.Services.AddAuthorization();


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseHttpsRedirection();

//先确定你是谁
app.UseAuthentication();

//再判断你有没有权限
app.UseAuthorization();

app.MapControllers();

app.Run();
