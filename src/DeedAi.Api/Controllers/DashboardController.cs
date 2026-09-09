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
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var rows = await ClientAccess.VisibleDocuments(db.Documents.AsNoTracking(), allowed).ToListAsync(cancellationToken);
        if (clientId is not null)
        {
            rows = rows.Where(x => x.ClientId == clientId).ToList();
        }

        if (from is not null)
        {
            rows = rows.Where(x => x.CreatedAt >= from).ToList();
        }

        if (to is not null)
        {
            rows = rows.Where(x => x.CreatedAt <= to).ToList();
        }

        return new DashboardCounts(
            rows.Count,
            rows.Count(x => x.Status == DocumentStatuses.Queued),
            rows.Count(x => x.Status == DocumentStatuses.Processing),
            rows.Count(x => x.Status == DocumentStatuses.Ready),
            rows.Count(x => x.Status == DocumentStatuses.Failed));
    }
}
