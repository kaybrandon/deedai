using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize(Policy = RolePolicies.CanEdit)]
[Route("api/sales")]
public sealed class SalesController(DeedAiDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SaleRow>>> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? assigneeUserId,
        [FromQuery] Guid? flagId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var query = DocumentFilters.Apply(
            DocumentFilters.WithReportIncludes(ClientAccess.VisibleDocuments(db.Documents.AsNoTracking(), allowed)),
            search, status, clientId, assigneeUserId, flagId, from, to);

        var rows = DocumentFilters.ApplyDates(await query.ToListAsync(cancellationToken), from, to);
        return rows
            .OrderByDescending(x => x.Fields?.InstrumentDate)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => new SaleRow(
                x.Id,
                x.Name,
                x.Client.Name,
                x.ClientId,
                x.Fields?.Grantor,
                x.Fields?.Grantee,
                x.Fields?.InstrumentDate,
                x.Fields?.Consideration,
                x.Fields?.ParcelId,
                x.Status,
                x.ReviewStatus,
                x.UpdatedAt))
            .ToList();
    }
}
