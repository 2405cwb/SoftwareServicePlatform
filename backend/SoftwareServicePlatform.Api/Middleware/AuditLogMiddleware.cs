using System.Diagnostics;
using System.Security.Claims;
using SoftwareServicePlatform.Api.Data;
using SoftwareServicePlatform.Api.Models;

namespace SoftwareServicePlatform.Api.Middleware
{
    /// <summary>
    /// 自动记录后台有副作用的操作：
    /// POST / PUT / PATCH / DELETE。
    ///
    /// 不记录请求 Body，避免密码、UpdateToken、更新激活码等敏感信息进入审计日志。
    /// </summary>
    public sealed class AuditLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _scopeFactory;

        public AuditLogMiddleware(
            RequestDelegate next,
            IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _scopeFactory = scopeFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var shouldAudit =
                context.User.Identity?.IsAuthenticated == true
                && IsWriteMethod(context.Request.Method)
                /*
                 * 客户端自动更新上报频率较高，不进入后台操作审计。
                 * 更新结果仍然保存在 DownloadRecord。
                 */
                && !context.Request.Path.StartsWithSegments("/api/client-updates");

            if (!shouldAudit)
            {
                await _next(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var statusCode = StatusCodes.Status500InternalServerError;

            try
            {
                await _next(context);
                statusCode = context.Response.StatusCode;
            }
            catch
            {
                statusCode = StatusCodes.Status500InternalServerError;
                throw;
            }
            finally
            {
                stopwatch.Stop();

                await TryWriteAuditAsync(
                    context,
                    statusCode,
                    stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task TryWriteAuditAsync(
            HttpContext context,
            int statusCode,
            long durationMs)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var dbContext =
                    scope.ServiceProvider
                        .GetRequiredService<AppDbContext>();

                int? userId = null;
                var userIdText =
                    context.User.FindFirstValue(
                        ClaimTypes.NameIdentifier);

                if (int.TryParse(userIdText, out var parsedUserId))
                {
                    userId = parsedUserId;
                }

                /*
                 * 只记录 Path，不记录 QueryString。
                 * 避免未来某些接口把临时票据/Token 放在查询参数时进入审计日志。
                 */
                var path =
                    context.Request.Path
                        .ToString();

                var userAgent =
                    context.Request.Headers.UserAgent
                        .ToString();

                dbContext.AuditLogs.Add(
                    new AuditLog
                    {
                        UserId = userId,
                        UserName =
                            context.User.FindFirstValue(
                                ClaimTypes.Name)
                            ?? string.Empty,
                        DisplayName =
                            context.User.FindFirstValue(
                                "displayName")
                            ?? string.Empty,
                        Role =
                            context.User.FindFirstValue(
                                ClaimTypes.Role)
                            ?? string.Empty,
                        HttpMethod =
                            Truncate(
                                context.Request.Method,
                                10),
                        Path =
                            Truncate(path, 500),
                        ActionName =
                            Truncate(
                                $"{context.Request.Method} {context.Request.Path}",
                                200),
                        StatusCode = statusCode,
                        DurationMs = durationMs,
                        IpAddress =
                            Truncate(
                                context.Connection
                                    .RemoteIpAddress?
                                    .ToString()
                                ?? string.Empty,
                                100),
                        UserAgent =
                            Truncate(
                                userAgent,
                                500),
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
                // 审计写入失败不能反过来影响真正业务请求。
            }
        }

        private static bool IsWriteMethod(string method)
        {
            return HttpMethods.IsPost(method)
                   || HttpMethods.IsPut(method)
                   || HttpMethods.IsPatch(method)
                   || HttpMethods.IsDelete(method);
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
