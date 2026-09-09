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
                sql = Check(detail.Sql),
                storage = Check(detail.Storage),
                queue = Check(detail.Queue),
                blob = Check(detail.Blob),
                documentIntelligence = Check(detail.DocumentIntelligence),
                ocrPipeline = Check(detail.OcrPipeline)
            },
            ocrQueue = new
            {
                depth = detail.OcrQueue.Depth,
                oldestWaitingAgeSeconds = detail.OcrQueue.OldestWaitingAgeSeconds,
                poisonCount = detail.OcrQueue.PoisonCount,
                failedCount = detail.OcrQueue.FailedCount,
                lastDiSuccessAt = detail.OcrQueue.LastDiSuccessAt,
                lastDiFailAt = detail.OcrQueue.LastDiFailAt
            }
        });
    }

    private static object Check(HealthCheckStatus item) => new
    {
        status = item.Status,
        reachable = item.Reachable,
        mode = item.Mode,
        detail = item.Detail,
        configured = item.Configured
    };
}
