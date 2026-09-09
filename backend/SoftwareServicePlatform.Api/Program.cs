using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;
using Microsoft.AspNetCore.Http.Features;
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

app.UseAuthorization();

app.MapControllers();

app.Run();
