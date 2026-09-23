using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareServicePlatform.Api.Services.Releases;

namespace SoftwareServicePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/release-preflight")]
    [Authorize(Roles = "Admin,Developer")]
    public class ReleasePreflightController : ControllerBase
    {
        private readonly ReleasePreflightService _service;

        public ReleasePreflightController(
            ReleasePreflightService service)
        {
            _service = service;
        }

        [HttpGet("{versionId:int}")]
        public async Task<IActionResult> Check(
            int versionId,
            CancellationToken cancellationToken)
        {
            var result =
                await _service.CheckAsync(
                    versionId,
                    cancellationToken);

            return Ok(result);
        }
    }
}
