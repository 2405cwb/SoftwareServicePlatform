using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareServicePlatform.Api.Services.ExternalNotifications;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 外部通知管理接口。
    ///
    /// 当前第一阶段主要用于：
    ///
    /// 测试各个外部通知渠道是否配置成功。
    ///
    /// 后面还可以继续增加：
    ///
    /// 查询已启用渠道
    /// 查询渠道状态
    /// 后台发送测试通知
    /// </summary>
    [ApiController]
    [Route("api/external-notifications")]
    [Authorize(Roles = "Admin,Developer")]
    public class ExternalNotificationsController
        : ControllerBase
    {
        private readonly IExternalNotificationService
            _externalNotificationService;


        public ExternalNotificationsController(
            IExternalNotificationService externalNotificationService)
        {
            _externalNotificationService =
                externalNotificationService;
        }


        /// <summary>
        /// 测试指定外部通知渠道。
        ///
        /// POST:
        ///
        /// /api/external-notifications/test
        ///
        /// 请求：
        ///
        /// {
        ///     "channel": "DingTalk"
        /// }
        ///
        /// 以后测试企业微信只需要：
        ///
        /// {
        ///     "channel": "WeCom"
        /// }
        ///
        /// 不需要再增加新的 Controller 接口。
        /// </summary>
        [HttpPost("test")]
        public async Task<IActionResult> Test(
            [FromBody] ExternalNotificationTestRequest request,
            CancellationToken cancellationToken)
        {
            /*
             * ==========================================
             * 1. 检查渠道
             * ==========================================
             */
            if (string.IsNullOrWhiteSpace(
                    request.Channel))
            {
                return BadRequest(
                    "请选择需要测试的通知渠道"
                );
            }


            /*
             * ==========================================
             * 2. 构造统一测试消息
             * ==========================================
             */
            var message =
                new ExternalNotificationMessage
                {
                    Title =
                        "外部通知接入测试",

                    Content =
                        "软件服务管理平台外部通知测试成功。\n"
                        +
                        $"测试时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",

                    Level =
                        "Info",

                    /*
                     * 测试消息暂时不需要指定接收用户。
                     *
                     * 后续做自动 @ 用户时，
                     * 再测试 Recipients。
                     */
                    Recipients = new(),

                    MentionAll = false
                };


            /*
             * ==========================================
             * 3. 调用统一外部通知服务
             * ==========================================
             *
             * Controller 不需要知道：
             *
             * DingTalkNotificationSender
             *
             * 的存在。
             */
            var success =
                await _externalNotificationService
                    .SendAsync(
                        request.Channel,
                        message,
                        cancellationToken
                    );


            /*
             * ==========================================
             * 4. 判断发送结果
             * ==========================================
             */
            if (!success)
            {
                return BadRequest(
                    $"外部通知发送失败：{request.Channel}，请查看后端日志"
                );
            }


            return Ok(
                new
                {
                    message =
                        $"{request.Channel} 测试消息发送成功"
                }
            );
        }
    }
}