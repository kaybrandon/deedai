using DeedAi.Api.Contracts;
using DeedAi.Domain.Entities;
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
        [FromQuery] Guid? flagId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var query = DocumentFilters.Apply(
            DocumentFilters.WithReportIncludes(ClientAccess.VisibleDocuments(db.Documents.AsNoTracking(), allowed)),
            search, status, clientId, assigneeUserId, flagId, from, to);

        var rows = DocumentFilters.ApplyDates(await query.ToListAsync(cancellationToken), from, to);
        var ordered = rows.OrderByDescending(x => x.UpdatedAt).ToList();
        var headers = new[]
        {
            "Name", "Client", "Status", "ReviewStatus", "DeedType", "Assignee", "UpdatedAt",
            "Grantor", "Grantee", "InstrumentDate", "Consideration", "ParcelId", "Flags"
        };
        var table = ordered.Select(ToRow).ToList();

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

        if (kind == "pdf")
        {
            return ReportPdf(ordered, headers, table);
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

    [HttpGet("documents/{id:guid}/pdf")]
    public async Task<IActionResult> ReviewedDeedPdf(Guid id, CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var document = await ClientAccess.VisibleDocuments(db.Documents.AsNoTracking(), allowed)
            .Include(x => x.Client)
            .Include(x => x.Assignee)
            .Include(x => x.Fields)
            .Include(x => x.Flags).ThenInclude(x => x.Flag)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound(new { message = "Deed not found or not visible for your Client access." });
        }

        if (document.Fields is null || (document.Fields.IsDraft && string.IsNullOrWhiteSpace(document.Fields.Grantor)))
        {
            return BadRequest(new { message = "This deed is not ready to export. Review and save fields first — a blank PDF is not returned." });
        }

        try
        {
            var bytes = DeedPdfWriter.ReviewedDeed(
                document.Name,
                document.Client.Name,
                document.Status,
                document.ReviewStatus,
                document.DeedType,
                document.Assignee?.DisplayName,
                document.UpdatedAt,
                [
                    ("Grantor", document.Fields.Grantor),
                    ("Grantee", document.Fields.Grantee),
                    ("Instrument date", document.Fields.InstrumentDate),
                    ("Consideration", document.Fields.Consideration),
                    ("Parcel ID", document.Fields.ParcelId),
                    ("Client", document.Fields.Client ?? document.Client.Name),
                    ("Notes", document.Fields.Notes)
                ],
                document.Flags.Select(f => f.Flag.Name).ToList());
            if (bytes.Length == 0)
            {
                return StatusCode(500, new { message = "PDF export produced no content. Try again or export CSV." });
            }

            return File(bytes, "application/pdf", SafeFileName(document.Name) + "-reviewed.pdf");
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Could not build the reviewed-deed PDF. Try again or export CSV." });
        }
    }

    private IActionResult ReportPdf(
        IReadOnlyList<Document> ordered,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> table)
    {
        if (ordered.Count == 0)
        {
            return BadRequest(new { message = "No rows for this report. Adjust filters or upload deeds — a blank PDF is not returned." });
        }

        try
        {
            var bytes = DeedPdfWriter.DocumentsReport(headers, table);
            if (bytes.Length == 0)
            {
                return StatusCode(500, new { message = "PDF export produced no content. Try again or export CSV." });
            }

            return File(bytes, "application/pdf", "deedai-report.pdf");
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Could not build the PDF export. Try again or export CSV." });
        }
    }

    private static IReadOnlyList<string?> ToRow(Document x) =>
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
    ];

    private static string SafeFileName(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        var cleaned = new string(stem.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "deed" : cleaned;
    }
}
