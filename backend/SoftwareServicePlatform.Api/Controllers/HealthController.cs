using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoftwareServicePlatform.Api.Data;

namespace SoftwareServicePlatform.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public HealthController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            CancellationToken cancellationToken)
        {
            var databaseOk =
                await _dbContext.Database
                    .CanConnectAsync(cancellationToken);

            if (!databaseOk)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        status = "degraded",
                        database = false,
                        utc = DateTime.UtcNow
                    });
            }

            return Ok(new
            {
                status = "ok",
                database = true,
                utc = DateTime.UtcNow
            });
        }
    }
}
