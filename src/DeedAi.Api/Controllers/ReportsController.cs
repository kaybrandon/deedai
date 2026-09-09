using DeedAi.Api.Contracts;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(DeedAiDbContext db) : ControllerBase
{
    [HttpGet("documents")]
    public async Task<IActionResult> Documents(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? assigneeUserId,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var query = ClientAccess.VisibleDocuments(db.Documents.AsNoTracking(), allowed)
            .Include(x => x.Client)
            .Include(x => x.Assignee)
            .Include(x => x.Fields)
            .Include(x => x.Flags).ThenInclude(x => x.Flag)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search) || (x.Fields != null && (x.Fields.Grantor!.Contains(search) || x.Fields.ParcelId!.Contains(search))));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status || x.ReviewStatus == status);
        }

        if (clientId is not null)
        {
            query = query.Where(x => x.ClientId == clientId);
        }

        if (assigneeUserId is not null)
        {
            query = query.Where(x => x.AssigneeUserId == assigneeUserId);
        }

        var rows = await query.ToListAsync(cancellationToken);
        var ordered = rows.OrderByDescending(x => x.UpdatedAt).ToList();
        var headers = new[]
        {
            "Name", "Client", "Status", "ReviewStatus", "DeedType", "Assignee", "UpdatedAt",
            "Grantor", "Grantee", "InstrumentDate", "Consideration", "ParcelId", "Flags"
        };
        var table = ordered.Select(x => (IReadOnlyList<string?>)
        [
            x.Name,
            x.Client.Name,
            x.Status,
            x.ReviewStatus,
            x.DeedType,
            x.Assignee?.DisplayName,
            x.UpdatedAt.ToString("u"),
            x.Fields?.Grantor,
            x.Fields?.Grantee,
            x.Fields?.InstrumentDate,
            x.Fields?.Consideration,
            x.Fields?.ParcelId,
            string.Join("; ", x.Flags.Select(f => f.Flag.Name))
        ]).ToList();

        var kind = (format ?? "json").ToLowerInvariant();
        if (kind == "csv")
        {
            return File(SpreadsheetWriter.ToCsv(headers, table), "text/csv", "deedai-report.csv");
        }

        if (kind == "xlsx")
        {
            return File(
                SpreadsheetWriter.ToXlsx("Documents", headers, table),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "deedai-report.xlsx");
        }

        return Ok(ordered.Select(x => new
        {
            x.Id,
            x.Name,
            Client = x.Client.Name,
            x.ClientId,
            x.Status,
            x.ReviewStatus,
            x.DeedType,
            Assignee = x.Assignee?.DisplayName,
            x.UpdatedAt,
            Fields = x.Fields is null ? null : new FieldDraft(
                x.Fields.Grantor, x.Fields.Grantee, x.Fields.InstrumentDate, x.Fields.Consideration,
                x.Fields.ParcelId, x.Fields.Client, x.Fields.Notes, x.Fields.IsDraft),
            Flags = x.Flags.Select(f => f.Flag.Name).ToList()
        }));
    }
}
