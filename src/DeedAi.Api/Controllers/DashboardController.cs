using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/dashboard")]
public sealed class DashboardController(DeedAiDbContext db) : ControllerBase
{
    private const string UnassignedLabel = "Unassigned";
    private const string TotalKey = "total";
    private const string TotalLabel = "Uploaded";

    [HttpGet("counts")]
    public async Task<ActionResult<DashboardCounts>> Counts(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        var rows = await LoadRows(from, to, clientId, includeAssignee: false, cancellationToken);
        return ToCounts(rows);
    }

    [HttpGet("charts/status-mix")]
    public async Task<ActionResult<DashboardStatusMix>> StatusMix(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        var rows = await LoadRows(from, to, clientId, includeAssignee: false, cancellationToken);
        var defs = await StatusLookupAsync(cancellationToken);
        return ToStatusMix(rows, defs);
    }

    [HttpGet("charts/by-user")]
    public async Task<ActionResult<DashboardByUser>> ByUser(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        var rows = await LoadRows(from, to, clientId, includeAssignee: true, cancellationToken);
        var defs = await StatusLookupAsync(cancellationToken);
        return ToByUser(rows, defs);
    }

    [HttpGet("charts/volume")]
    public async Task<ActionResult<DashboardVolume>> Volume(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        var rows = await LoadRows(from, to, clientId, includeAssignee: false, cancellationToken);
        var defs = await StatusLookupAsync(cancellationToken);
        return ToVolume(rows, defs, from, to);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? clientId,
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate,
        CancellationToken cancellationToken)
    {
        var rows = await LoadRows(from, to, clientId, includeAssignee: true, cancellationToken);
        if (rows.Count == 0)
        {
            return BadRequest(new
            {
                message = "No dashboard data for these filters. Adjust the date range or Client — a blank PDF is not returned."
            });
        }

        try
        {
            var defs = await StatusLookupAsync(cancellationToken);
            var counts = ToCounts(rows);
            var mix = ToStatusMix(rows, defs);
            var byUser = ToByUser(rows, defs);
            var volume = ToVolume(rows, defs, from, to);
            var clientFilter = await ResolveClientFilterAsync(clientId, rows, cancellationToken);
            var title = clientFilter == "All clients" ? "Deed AI" : $"{clientFilter} Deed AI";
            var rangeFrom = from ?? rows.Min(x => x.CreatedAt);
            var rangeTo = to ?? rows.Max(x => x.CreatedAt);
            var bytes = DeedPdfWriter.Dashboard(
                title,
                clientFilter,
                rangeFrom,
                rangeTo,
                DateTimeOffset.UtcNow,
                counts.Uploaded,
                counts.Queued,
                counts.Processing,
                counts.Ready,
                counts.Failed,
                mix.Series.Select(s => (s.Label, s.Count)).ToList(),
                byUser.Labels.ToList(),
                byUser.Series.Select(s => (s.Label, s.Data)).ToList(),
                volume.Labels.ToList(),
                volume.Series.Select(s => (s.Label, s.Data)).ToList());
            if (bytes.Length == 0)
            {
                return StatusCode(500, new { message = "PDF export produced no content. Try again or print the dashboard." });
            }

            return File(bytes, "application/pdf", DeedPdfWriter.DashboardFileName(rangeFrom, rangeTo, fromDate, toDate));
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Could not build the dashboard PDF. Try again or print the dashboard." });
        }
    }

    private async Task<IReadOnlyList<Document>> LoadRows(
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? clientId,
        bool includeAssignee,
        CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        IQueryable<Document> query = db.Documents.AsNoTracking();
        if (includeAssignee)
        {
            query = query.Include(x => x.Assignee);
        }

        query = ClientAccess.VisibleDocuments(query, allowed);
        if (clientId is not null)
        {
            query = query.Where(x => x.ClientId == clientId);
        }

        var rows = await query.ToListAsync(cancellationToken);
        return DocumentFilters.ApplyDates(rows, from, to);
    }

    private async Task<IReadOnlyDictionary<string, StatusDefinition>> StatusLookupAsync(
        CancellationToken cancellationToken)
    {
        var defs = await db.StatusDefinitions.AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        return defs
            .GroupBy(x => x.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    private async Task<string> ResolveClientFilterAsync(
        Guid? clientId,
        IReadOnlyList<Document> rows,
        CancellationToken cancellationToken)
    {
        Guid? id = clientId;
        if (id is null)
        {
            var distinct = rows.Select(x => x.ClientId).Distinct().ToList();
            if (distinct.Count == 1)
            {
                id = distinct[0];
            }
        }

        if (id is null)
        {
            return "All clients";
        }

        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        if (!ClientAccess.CanSee(allowed, id.Value))
        {
            return "All clients";
        }

        var name = await db.Clients.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(name) ? "All clients" : name;
    }

    private static DashboardCounts ToCounts(IReadOnlyList<Document> rows) =>
        new(
            rows.Count,
            rows.Count(x => x.Status == DocumentStatuses.Queued),
            rows.Count(x => x.Status == DocumentStatuses.Processing),
            rows.Count(x => x.Status == DocumentStatuses.Ready),
            rows.Count(x => x.Status == DocumentStatuses.Failed));

    private static DashboardStatusMix ToStatusMix(
        IReadOnlyList<Document> rows,
        IReadOnlyDictionary<string, StatusDefinition> defs)
    {
        if (rows.Count == 0)
        {
            return new DashboardStatusMix(0, []);
        }

        var series = DocumentStatuses.All
            .Select(status => Slice(status, rows.Count(x => x.Status == status), defs))
            .ToList();
        return new DashboardStatusMix(rows.Count, series);
    }

    private static DashboardByUser ToByUser(
        IReadOnlyList<Document> rows,
        IReadOnlyDictionary<string, StatusDefinition> defs)
    {
        if (rows.Count == 0)
        {
            return new DashboardByUser([], [], []);
        }

        var columns = rows
            .GroupBy(x => x.AssigneeUserId)
            .Select(group =>
            {
                var sample = group.First();
                var displayName = sample.Assignee?.DisplayName
                    ?? (sample.AssigneeUserId is null ? UnassignedLabel : "Unknown");
                return new DashboardUserColumn(sample.AssigneeUserId, displayName);
            })
            .OrderBy(x => x.UserId is null)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.UserId)
            .ToList();

        var series = StatusSeries(columns, defs, (column, status) =>
            rows.Count(x => x.AssigneeUserId == column.UserId && x.Status == status));
        return new DashboardByUser(
            columns.Select(x => x.DisplayName).ToList(),
            columns,
            series);
    }

    private static DashboardVolume ToVolume(
        IReadOnlyList<Document> rows,
        IReadOnlyDictionary<string, StatusDefinition> defs,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (rows.Count == 0)
        {
            return new DashboardVolume([], []);
        }

        var labels = VolumeLabels(rows, from, to);
        var byDate = rows
            .GroupBy(x => x.CreatedAt.UtcDateTime.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var total = new DashboardStackedSeries(
            TotalKey,
            TotalLabel,
            labels.Select(label =>
            {
                var day = DateTime.Parse(label, System.Globalization.CultureInfo.InvariantCulture);
                return byDate.TryGetValue(day, out var dayRows) ? dayRows.Count : 0;
            }).ToList(),
            null);

        var statusSeries = StatusSeries(labels, defs, (label, status) =>
        {
            var day = DateTime.Parse(label, System.Globalization.CultureInfo.InvariantCulture);
            return byDate.TryGetValue(day, out var dayRows)
                ? dayRows.Count(x => x.Status == status)
                : 0;
        });

        return new DashboardVolume(labels, [total, .. statusSeries]);
    }

    private static DashboardStatusSlice Slice(
        string status,
        int count,
        IReadOnlyDictionary<string, StatusDefinition> defs)
    {
        defs.TryGetValue(status, out var def);
        return new DashboardStatusSlice(status, def?.DisplayName ?? status, count, def?.Color);
    }

    private static IReadOnlyList<DashboardStackedSeries> StatusSeries<T>(
        IReadOnlyList<T> columns,
        IReadOnlyDictionary<string, StatusDefinition> defs,
        Func<T, string, int> countFor)
    {
        return DocumentStatuses.All.Select(status =>
        {
            defs.TryGetValue(status, out var def);
            return new DashboardStackedSeries(
                status,
                def?.DisplayName ?? status,
                columns.Select(column => countFor(column, status)).ToList(),
                def?.Color);
        }).ToList();
    }

    private static IReadOnlyList<string> VolumeLabels(
        IReadOnlyList<Document> rows,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var dataMin = rows.Min(x => x.CreatedAt.UtcDateTime.Date);
        var dataMax = rows.Max(x => x.CreatedAt.UtcDateTime.Date);
        var start = from?.UtcDateTime.Date ?? dataMin;
        var end = to?.UtcDateTime.Date ?? dataMax;
        if (end < start)
        {
            return [];
        }

        var days = (end - start).Days + 1;
        if (days > 366)
        {
            return rows
                .Select(x => x.CreatedAt.UtcDateTime.Date)
                .Distinct()
                .OrderBy(x => x)
                .Select(FormatDay)
                .ToList();
        }

        return Enumerable.Range(0, days)
            .Select(offset => FormatDay(start.AddDays(offset)))
            .ToList();
    }

    private static string FormatDay(DateTime day) =>
        day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
