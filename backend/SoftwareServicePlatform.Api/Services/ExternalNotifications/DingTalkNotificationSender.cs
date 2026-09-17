using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace SoftwareServicePlatform.Api.Services.ExternalNotifications
{
    /// <summary>
    /// 钉钉机器人通知发送器。
    ///
    /// 职责非常单一：
    ///
    /// ExternalNotificationMessage
    ///             ↓
    /// 转换成钉钉要求的 JSON
    ///             ↓
    /// POST 到 Webhook
    ///             ↓
    /// 判断钉钉是否发送成功
    ///
    /// 注意：
    ///
    /// 这个类完全不知道什么是：
    ///
    /// 工单
    /// SLA
    /// 软件版本
    /// 客户
    ///
    /// 它只负责“怎么把一条通知发到钉钉”。
    /// </summary>
    public class DingTalkNotificationSender
        : IExternalNotificationSender
    {
        /// <summary>
        /// HttpClient 由 ASP.NET Core 的
        /// IHttpClientFactory 负责创建和管理。
        ///
        /// 不要在每次发送时自己 new HttpClient()。
        /// </summary>
        private readonly HttpClient _httpClient;


        /// <summary>
        /// appsettings 中读取到的钉钉配置。
        /// </summary>
        private readonly DingTalkOptions _options;


        /// <summary>
        /// 日志服务。
        ///
        /// 外部通知失败时记录日志，
        /// 方便以后排查。
        /// </summary>
        private readonly ILogger<DingTalkNotificationSender>
            _logger;


        /// <summary>
        /// 当前 Sender 的唯一渠道名称。
        ///
        /// ExternalNotificationService 后面就是根据：
        ///
        /// "DingTalk"
        ///
        /// 找到当前这个 Sender。
        /// </summary>
        public string Channel => "DingTalk";


        /// <summary>
        /// 构造函数。
        ///
        /// HttpClient、配置和日志
        /// 都由 ASP.NET Core DI 自动注入。
        /// </summary>
        public DingTalkNotificationSender(
            HttpClient httpClient,
            IOptions<DingTalkOptions> options,
            ILogger<DingTalkNotificationSender> logger)
        {
            _httpClient = httpClient;

            /*
             * IOptions<T>.Value
             * 就是从配置文件绑定后的实际配置对象。
             */
            _options = options.Value;

            _logger = logger;
        }


        /// <summary>
        /// 向钉钉发送一条消息。
        /// </summary>
        public async Task<bool> SendAsync(
            ExternalNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            /*
             * ==========================================
             * 1. 检查功能是否启用
             * ==========================================
             */
            if (!_options.Enabled)
            {
                _logger.LogDebug(
                    "钉钉通知渠道当前未启用"
                );

                return false;
            }


            /*
             * ==========================================
             * 2. 检查 Webhook
             * ==========================================
             *
             * 注意：
             * Webhook 属于敏感信息。
             *
             * 即使配置错误，
             * 日志里也不要直接打印 Webhook。
             */
            if (string.IsNullOrWhiteSpace(
                    _options.Webhook))
            {
                _logger.LogWarning(
                    "钉钉通知已启用，但没有配置 Webhook"
                );

                return false;
            }


            /*
             * ==========================================
             * 3. 检查消息内容
             * ==========================================
             */
            if (
                string.IsNullOrWhiteSpace(message.Title)
                &&
                string.IsNullOrWhiteSpace(message.Content)
            )
            {
                _logger.LogWarning(
                    "准备发送钉钉通知，但消息内容为空"
                );

                return false;
            }


            /*
             * ==========================================
             * 4. 生成钉钉最终看到的文字
             * ==========================================
             */
            var finalContent =
                BuildTextContent(message);


            /*
             * ==========================================
             * 5. 构造钉钉 text 消息 JSON
             * ==========================================
             *
             * 最终会序列化成类似：
             *
             * {
             *   "msgtype": "text",
             *   "text": {
             *      "content": "..."
             *   },
             *   "at": {
             *      "isAtAll": false
             *   }
             * }
             */
            var request =
                new DingTalkTextRequest
                {
                    MsgType = "text",

                    Text =
                        new DingTalkTextContent
                        {
                            Content = finalContent
                        },

                    At =
                        new DingTalkAtOptions
                        {
                            /*
                             * 第一版不 @所有人。
                             *
                             * 否则以后通知多了，
                             * 群里体验会很差。
                             */
                            IsAtAll = false
                        }
                };


            try
            {
                /*
                 * ==========================================
                 * 6. POST 到钉钉 Webhook
                 * ==========================================
                 *
                 * PostAsJsonAsync 会自动：
                 *
                 * 对象 → JSON
                 *
                 * 并设置：
                 *
                 * Content-Type: application/json
                 */
                using var response =
                    await _httpClient.PostAsJsonAsync(
                        _options.Webhook,
                        request,
                        cancellationToken
                    );


                /*
                 * ==========================================
                 * 7. 解析钉钉返回结果
                 * ==========================================
                 *
                 * 钉钉成功通常返回：
                 *
                 * {
                 *     "errcode": 0,
                 *     "errmsg": "ok"
                 * }
                 */
                DingTalkResponse? result;

                try
                {
                    result =
                        await response.Content
                            .ReadFromJsonAsync<DingTalkResponse>(
                                cancellationToken:
                                    cancellationToken
                            );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "无法解析钉钉机器人返回结果。HttpStatus={HttpStatus}",
                        response.StatusCode
                    );

                    return false;
                }


                /*
                 * ==========================================
                 * 8. HTTP 层是否成功
                 * ==========================================
                 */
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "钉钉 HTTP 请求失败。HttpStatus={HttpStatus}",
                        response.StatusCode
                    );

                    return false;
                }


                /*
                 * ==========================================
                 * 9. 钉钉业务层是否成功
                 * ==========================================
                 *
                 * HTTP 200 并不代表消息一定发送成功。
                 *
                 * 还必须判断：
                 *
                 * errcode == 0
                 */
                if (
                    result == null
                    ||
                    result.ErrCode != 0
                )
                {
                    _logger.LogWarning(
                        "钉钉消息发送失败。ErrCode={ErrCode}, ErrMsg={ErrMsg}",
                        result?.ErrCode,
                        result?.ErrMsg
                    );

                    return false;
                }


                /*
                 * ==========================================
                 * 10. 真正发送成功
                 * ==========================================
                 */
                _logger.LogInformation(
                    "钉钉通知发送成功。Title={Title}",
                    message.Title
                );

                return true;
            }
            catch (Exception ex)
            {
                /*
                 * ==========================================
                 * 11. 外部网络异常兜底
                 * ==========================================
                 *
                 * 可能出现：
                 *
                 * 钉钉服务不可用
                 * 网络断开
                 * DNS 错误
                 * 请求超时
                 *
                 * 最重要的一点：
                 *
                 * 钉钉发送失败，
                 * 以后绝对不能导致：
                 *
                 * 工单创建失败
                 * 版本发布失败
                 * 数据保存失败
                 *
                 * 所以这里只：
                 *
                 * 记录日志
                 * 返回 false
                 *
                 * 不向业务层继续抛异常。
                 */
                _logger.LogError(
                    ex,
                    "发送钉钉通知发生异常"
                );

                return false;
            }
        }


        /// <summary>
        /// 把统一消息模型转换成
        /// 钉钉最终显示的文字。
        /// </summary>
        private string BuildTextContent(
            ExternalNotificationMessage message)
        {
            var parts =
                new List<string>();


            /*
             * 如果机器人使用“关键词”安全模式，
             * 消息里面必须包含关键词。
             */
            if (!string.IsNullOrWhiteSpace(
                    _options.Keyword))
            {
                parts.Add(
                    $"【{_options.Keyword}】"
                );
            }


            /*
             * 把系统内部 Level
             * 转成用户容易理解的中文。
             */
            var levelText =
                message.Level switch
                {
                    "Warning" => "警告",

                    "Error" => "错误",

                    _ => "通知"
                };


            parts.Add(
                $"【{levelText}】"
            );


            /*
             * 标题。
             */
            if (!string.IsNullOrWhiteSpace(
                    message.Title))
            {
                parts.Add(
                    message.Title.Trim()
                );
            }


            /*
             * 正文。
             */
            if (!string.IsNullOrWhiteSpace(
                    message.Content))
            {
                parts.Add(
                    message.Content.Trim()
                );
            }


            /*
             * 如果存在业务地址，
             * 第一版先直接显示出来。
             *
             * 后面升级 Markdown 后，
             * 可以做成真正的超链接。
             */
            if (!string.IsNullOrWhiteSpace(
                    message.TargetUrl))
            {
                parts.Add(
                    $"详情：{message.TargetUrl}"
                );
            }


            return string.Join(
                "\n\n",
                parts
            );
        }


        /*
         * ==============================================
         * 下面是钉钉专用 DTO
         * ==============================================
         *
         * 这些类仅仅是为了匹配钉钉 JSON 格式。
         *
         * 不应该暴露给：
         *
         * Controller
         * NotificationService
         * 工单业务
         *
         * 所以放在 Sender 内部即可。
         */


        private class DingTalkTextRequest
        {
            [JsonPropertyName("msgtype")]
            public string MsgType { get; set; }
                = "text";


            [JsonPropertyName("text")]
            public DingTalkTextContent Text { get; set; }
                = new();


            [JsonPropertyName("at")]
            public DingTalkAtOptions At { get; set; }
                = new();
        }


        private class DingTalkTextContent
        {
            [JsonPropertyName("content")]
            public string Content { get; set; }
                = string.Empty;
        }


        private class DingTalkAtOptions
        {
            [JsonPropertyName("isAtAll")]
            public bool IsAtAll { get; set; }
        }


        private class DingTalkResponse
        {
            [JsonPropertyName("errcode")]
            public int ErrCode { get; set; }


            [JsonPropertyName("errmsg")]
            public string ErrMsg { get; set; }
                = string.Empty;
        }
    }
}