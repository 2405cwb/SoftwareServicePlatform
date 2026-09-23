using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SoftwareServicePlatform.Api.Services.Releases
{
    /// <summary>
    /// 无论从哪个前端入口发起发布，都在真正进入 PublishVersion 前统一检查。
    /// </summary>
    public sealed class PublishPreflightFilter : IAsyncActionFilter
    {
        private readonly ReleasePreflightService _service;

        public PublishPreflightFilter(
            ReleasePreflightService service)
        {
            _service = service;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            
                context.ActionDescriptor.RouteValues.TryGetValue(
                "controller",
                out var controller);

            
                context.ActionDescriptor.RouteValues
                    .TryGetValue(
                "action",
                out var action);

            if (
                string.Equals(
                    controller,
                    "SoftwareVersions",
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    action,
                    "PublishVersion",
                    StringComparison.OrdinalIgnoreCase)
                && context.ActionArguments.TryGetValue(
                    "id",
                    out var idValue)
                && idValue is int versionId)
            {
                var result =
                    await _service.CheckAsync(
                        versionId,
                        context.HttpContext.RequestAborted);

                if (!result.CanPublish)
                {
                    context.Result =
                        new BadRequestObjectResult(
                            new
                            {
                                message = "发布前检查未通过",
                                errors = result.Errors,
                                warnings = result.Warnings
                            });

                    return;
                }
            }

            await next();
        }
    }
}
