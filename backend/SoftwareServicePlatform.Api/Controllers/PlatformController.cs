using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 平台公共信息。
    ///
    /// 这些信息不是敏感数据，
    /// 登录页面也需要使用，
    /// 所以允许未登录访问。
    /// </summary>
    [ApiController]
    [Route("api/platform")]
    public class PlatformController : ControllerBase
    {
        private readonly IConfiguration _configuration;


        public PlatformController(
            IConfiguration configuration)
        {
            _configuration = configuration;
        }


        /// <summary>
        /// 获取平台基本信息。
        ///
        /// GET /api/platform/info
        /// </summary>
        [AllowAnonymous]
        [HttpGet("info")]
        public IActionResult GetPlatformInfo()
        {
            /*
             * 从 appsettings.json：
             *
             * Platform:Title
             *
             * 读取平台标题。
             *
             * 如果配置不存在，
             * 使用默认名称，
             * 防止页面出现空标题。
             */
            var title =
                _configuration["Platform:Title"]
                ?? "软件服务管理平台";


            var companyName =
                _configuration["Platform:CompanyName"]
                ?? string.Empty;


            return Ok(new
            {
                title,
                companyName
            });
        }
    }
}