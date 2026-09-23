using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SoftwareServicePlatform.Api.Controllers
{
    /// <summary>
    /// 旧版“客户 + 软件共享 UpdateToken”接口已停用。
    /// </summary>
    [ApiController]
    [Route("api/client-update-admin")]
    [Authorize(Roles = "Admin")]
    public class ClientUpdateAdminController : ControllerBase
    {
        private IActionResult Gone()
        {
            return StatusCode(
                StatusCodes.Status410Gone,
                "旧版共享 UpdateToken 已停用，请使用“更新设备授权”功能。");
        }

        [HttpGet("bindings")]
        public IActionResult GetBindings() => Gone();

        [HttpPost("bindings/{customerSoftwareId:int}/token")]
        public IActionResult GenerateToken(int customerSoftwareId) => Gone();

        [HttpPost("bindings/{customerSoftwareId:int}/revoke")]
        public IActionResult RevokeToken(int customerSoftwareId) => Gone();

        [HttpPost("bindings/{customerSoftwareId:int}/enable")]
        public IActionResult EnableToken(int customerSoftwareId) => Gone();
    }
}
