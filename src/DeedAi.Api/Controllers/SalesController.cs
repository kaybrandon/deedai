using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Domain.Entities;
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
    public async Task<ActionResult<SalesPageResponse>> List(
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
        if (clientId is { } filterClient && !ClientAccess.CanSee(allowed, filterClient))
        {
            return Forbid();
        }

        var configs = await db.SoftwareClientConfigs.AsNoTracking().ToListAsync(cancellationToken);
        var codes = await db.SalesTabCodes.AsNoTracking()
            .Include(x => x.Client)
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var scopedConfigs = configs
            .Where(x => ClientAccess.CanSee(allowed, x.ClientId) && (clientId is null || x.ClientId == clientId))
            .ToList();
        var display = scopedConfigs.Any(x => x.DisplaySalesTab);
        var threshold = scopedConfigs
            .Where(x => x.DisplaySalesTab)
            .Select(x => (decimal?)x.ConsiderationThreshold)
            .DefaultIfEmpty(null)
            .Min();

        var visibleCodes = codes
            .Where(x => x.ClientId is null || ClientAccess.CanSee(allowed, x.ClientId.Value))
            .Where(x => clientId is null || x.ClientId is null || x.ClientId == clientId)
            .Select(x => new SalesTabCodeItem(
                x.Id, x.ClientId, x.Client?.Name, x.Code, x.Label,
                x.MinConsideration, x.MaxConsideration, x.IsActive, x.SortOrder))
            .ToList();

        if (!display)
        {
            return new SalesPageResponse(false, threshold, visibleCodes, []);
        }

        var query = DocumentFilters.Apply(
            DocumentFilters.WithReportIncludes(ClientAccess.VisibleDocuments(db.Documents.AsNoTracking(), allowed)),
            search, status, clientId, assigneeUserId, flagId, from, to);

        var rows = DocumentFilters.ApplyDates(await query.ToListAsync(cancellationToken), from, to);
        var sales = rows
            .Select(doc =>
            {
                var config = configs.FirstOrDefault(x => x.ClientId == doc.ClientId);
                var amount = SalesTabRules.ParseConsideration(doc.Fields?.Consideration);
                if (!SalesTabRules.MeetsThreshold(config, amount))
                {
                    return null;
                }

                var code = SalesTabRules.ResolveCode(doc.SalesTabCode, codes, doc.ClientId, amount);
                return new SaleRow(
                    doc.Id,
                    doc.Name,
                    doc.Client.Name,
                    doc.ClientId,
                    doc.Fields?.Grantor,
                    doc.Fields?.Grantee,
                    doc.Fields?.InstrumentDate,
                    doc.Fields?.Consideration,
                    doc.Fields?.ParcelId,
                    code,
                    doc.Status,
                    doc.ReviewStatus,
                    doc.UpdatedAt);
            })
            .Where(x => x is not null)
            .Cast<SaleRow>()
            .OrderByDescending(x => x.InstrumentDate)
            .ThenByDescending(x => x.UpdatedAt)
            .ToList();

        return new SalesPageResponse(true, threshold, visibleCodes, sales);
    }

    [HttpPut("{id:guid}/code")]
    public async Task<ActionResult<SaleRow>> AssignCode(
        Guid id,
        [FromBody] AssignSalesTabCodeRequest request,
        CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var document = await ClientAccess.VisibleDocuments(db.Documents, allowed)
            .Include(x => x.Client)
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        var config = await db.SoftwareClientConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClientId == document.ClientId, cancellationToken);
        if (config is not { DisplaySalesTab: true })
        {
            return BadRequest(new { message = "Display Sales Tab is off for this Client. An Admin can enable it on the Software page." });
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            document.SalesTabCode = null;
        }
        else
        {
            var code = request.Code.Trim().ToUpperInvariant();
            var match = await db.SalesTabCodes.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.IsActive
                         && x.Code == code
                         && (x.ClientId == null || x.ClientId == document.ClientId),
                    cancellationToken);
            if (match is null)
            {
                return BadRequest(new { message = "Unknown or inactive Sales Tab code." });
            }

            document.SalesTabCode = match.Code;
        }

        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new SaleRow(
            document.Id,
            document.Name,
            document.Client.Name,
            document.ClientId,
            document.Fields?.Grantor,
            document.Fields?.Grantee,
            document.Fields?.InstrumentDate,
            document.Fields?.Consideration,
            document.Fields?.ParcelId,
            document.SalesTabCode,
            document.Status,
            document.ReviewStatus,
            document.UpdatedAt);
    }
}
