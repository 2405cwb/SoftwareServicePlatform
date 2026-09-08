using Microsoft.AspNetCore.Mvc;

namespace SoftwareServicePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController:ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { message= "软件服务管理平台后端运行正常" });
        }
    }
}
