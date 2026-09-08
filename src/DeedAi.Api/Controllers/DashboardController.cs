using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(DeedAiDbContext db) : ControllerBase
{
    [HttpGet("counts")]
    public async Task<ActionResult<DashboardCounts>> Counts(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        var query = db.Documents.AsQueryable();
        if (from is not null)
        {
            query = query.Where(x => x.CreatedAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(x => x.CreatedAt <= to);
        }

        if (clientId is not null)
        {
            query = query.Where(x => x.ClientId == clientId);
        }

        var uploaded = await query.CountAsync(cancellationToken);
        var queued = await query.CountAsync(x => x.Status == DocumentStatuses.Queued, cancellationToken);
        var processing = await query.CountAsync(x => x.Status == DocumentStatuses.Processing, cancellationToken);
        var ready = await query.CountAsync(x => x.Status == DocumentStatuses.Ready, cancellationToken);
        var failed = await query.CountAsync(x => x.Status == DocumentStatuses.Failed, cancellationToken);
        return new DashboardCounts(uploaded, queued, processing, ready, failed);
    }
}
