using System.Security.Claims;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Middleware
{
    /// <summary>
    /// 捕获未处理异常、HTTP 5xx 和登录失败，供管理员运维中心查看。
    /// </summary>
    public sealed class SystemEventMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _scopeFactory;

        public SystemEventMiddleware(
            RequestDelegate next,
            IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _scopeFactory = scopeFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);

                if (context.Response.StatusCode >= 500)
                {
                    await TryWriteAsync(
                        context,
                        "Error",
                        "Http5xx",
                        $"接口返回 HTTP {context.Response.StatusCode}",
                        string.Empty,
                        context.Response.StatusCode);
                }
                else if (
                    context.Request.Path.StartsWithSegments("/api/auth/login")
                    && HttpMethods.IsPost(context.Request.Method)
                    && context.Response.StatusCode >= 400)
                {
                    await TryWriteAsync(
                        context,
                        "Warning",
                        "Auth.LoginFailed",
                        "登录失败",
                        "未记录登录请求 Body，避免密码等敏感信息进入日志。",
                        context.Response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                await TryWriteAsync(
                    context,
                    "Error",
                    "UnhandledException",
                    ex.Message,
                    ex.ToString(),
                    StatusCodes.Status500InternalServerError);

                throw;
            }
        }

        private async Task TryWriteAsync(
            HttpContext context,
            string level,
            string source,
            string message,
            string detail,
            int statusCode)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var dbContext =
                    scope.ServiceProvider
                        .GetRequiredService<AppDbContext>();

                dbContext.SystemEventLogs.Add(
                    new SystemEventLog
                    {
                        Level = Truncate(level, 20),
                        Source = Truncate(source, 100),
                        Message = Truncate(message, 1000),
                        Detail = Truncate(detail, 8000),
                        Path =
                            Truncate(
                                context.Request.Path
                                    .ToString(),
                                500),
                        HttpMethod =
                            Truncate(
                                context.Request.Method,
                                10),
                        StatusCode = statusCode,
                        UserName =
                            Truncate(
                                context.User.FindFirstValue(
                                    ClaimTypes.Name)
                                ?? string.Empty,
                                100),
                        IpAddress =
                            Truncate(
                                context.Connection
                                    .RemoteIpAddress?
                                    .ToString()
                                ?? string.Empty,
                                100),
                        TraceId =
                            Truncate(
                                context.TraceIdentifier,
                                100),
                        CreatedAt = DateTime.UtcNow
                    });

                await dbContext.SaveChangesAsync();
            }
            catch
            {
                // 错误日志本身不能制造新的业务异常。
            }
        }

        private static string Truncate(
            string value,
            int maxLength)
        {
            value ??= string.Empty;

            return value.Length <= maxLength
                ? value
                : value[..maxLength];
        }
    }
}
