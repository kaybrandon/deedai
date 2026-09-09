using DeedAi.Domain;
using DeedAi.Infrastructure.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeedAi.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class HealthController(RuntimeHealth health) : ControllerBase
{
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(RuntimeHealth.Shallow());

    [HttpGet("health/detail")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> Detail(CancellationToken cancellationToken)
    {
        var detail = await health.DetailAsync(cancellationToken);
        return Ok(new
        {
            status = detail.Status,
            product = detail.Product,
            checks = new
            {
                sql = new { status = detail.Sql.Status, reachable = detail.Sql.Reachable, mode = detail.Sql.Mode },
                storage = new { status = detail.Storage.Status, reachable = detail.Storage.Reachable, mode = detail.Storage.Mode },
                queue = new { status = detail.Queue.Status, reachable = detail.Queue.Reachable, mode = detail.Queue.Mode }
            }
        });
    }
}
