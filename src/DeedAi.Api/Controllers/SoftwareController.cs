using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class SoftwareController(DeedAiDbContext db, ISoftwareClient software) : ControllerBase
{
    [HttpGet("software/lookup")]
    public async Task<ActionResult<SoftwareLookupResponse>> Lookup(
        [FromQuery] string? parcelId,
        [FromQuery] string? grantor,
        [FromQuery] string? grantee,
        [FromQuery] string? client,
        [FromQuery] string? instrumentDate,
        [FromQuery] string? deedType,
        CancellationToken cancellationToken)
    {
        var query = new SoftwareLookupQuery(parcelId, grantor, grantee, client, instrumentDate, deedType);
        if (!HasKeyField(query))
        {
            return BadRequest(new { message = "Lookup Software by parcel ID, grantor, grantee, Client, or instrument date." });
        }

        var result = await software.LookupAsync(query, cancellationToken);
        if (result is null)
        {
            return NotFound(new { message = "No Software record found for those key fields." });
        }

        return ToResponse(result);
    }

    [HttpPost("documents/{id:guid}/software/lookup")]
    public async Task<ActionResult<SoftwareLookupResponse>> LookupDocument(Guid id, CancellationToken cancellationToken)
    {
        var document = await LoadVisible(id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        var query = QueryFor(document);
        if (!HasKeyField(query))
        {
            return BadRequest(new { message = "Add a Parcel ID, grantor, grantee, or Client before looking up Software." });
        }

        var result = await software.LookupAsync(query, cancellationToken);
        RecordSync(document, "Lookup", result is null ? "Miss" : "Ok", result is null ? "No Software record for those key fields." : null, result?.SoftwareRecordId);
        await db.SaveChangesAsync(cancellationToken);
        if (result is null)
        {
            return NotFound(new { message = "No Software record found for those key fields." });
        }

        return ToResponse(result);
    }

    [HttpPost("documents/{id:guid}/software/push")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public Task<ActionResult<SoftwarePushResponse>> Push(Guid id, CancellationToken cancellationToken) =>
        PushCore(id, "Push", cancellationToken);

    [HttpPost("documents/{id:guid}/software/retry")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public Task<ActionResult<SoftwarePushResponse>> Retry(Guid id, CancellationToken cancellationToken) =>
        PushCore(id, "Retry", cancellationToken);

    private async Task<ActionResult<SoftwarePushResponse>> PushCore(Guid id, string direction, CancellationToken cancellationToken)
    {
        var document = await LoadVisible(id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        var map = document.DeedType is null
            ? null
            : await db.DeedTypeMaps.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DeedType == document.DeedType && x.IsActive, cancellationToken);

        var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (document.Fields is not null)
        {
            TryMap(mapped, map, "grantor", document.Fields.Grantor);
            TryMap(mapped, map, "grantee", document.Fields.Grantee);
            TryMap(mapped, map, "parcelId", document.Fields.ParcelId);
            TryMap(mapped, map, "consideration", document.Fields.Consideration);
            TryMap(mapped, map, "instrumentDate", document.Fields.InstrumentDate);
            TryMap(mapped, map, "client", document.Fields.Client ?? document.Client.Name);
            TryMap(mapped, map, "notes", document.Fields.Notes);
        }

        var result = await software.PushAsync(
            new SoftwarePushRequest(
                document.Id,
                document.Fields?.ParcelId,
                document.DeedType,
                map?.SoftwareCode,
                document.Fields?.Grantor,
                document.Fields?.Grantee,
                document.Fields?.InstrumentDate,
                document.Fields?.Consideration,
                document.Fields?.Client ?? document.Client.Name,
                document.Fields?.Notes,
                mapped),
            cancellationToken);

        RecordSync(document, direction, result.Succeeded ? "Ok" : "Failed", result.Succeeded ? null : result.Message, result.SoftwareRecordId);
        await db.SaveChangesAsync(cancellationToken);
        return new SoftwarePushResponse(
            result.Succeeded,
            result.SoftwareRecordId,
            result.Message,
            document.LastSoftwareSyncAt,
            document.LastSoftwareSyncStatus,
            document.LastSoftwareSyncFailReason);
    }

    private async Task<Document?> LoadVisible(Guid id, CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        return await ClientAccess.VisibleDocuments(db.Documents, allowed)
            .Include(x => x.Client)
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private void RecordSync(Document document, string direction, string status, string? failReason, string? softwareRecordId)
    {
        document.LastSoftwareSyncAt = DateTimeOffset.UtcNow;
        document.LastSoftwareSyncDirection = direction;
        document.LastSoftwareSyncStatus = status;
        document.LastSoftwareSyncFailReason = failReason;
        if (!string.IsNullOrWhiteSpace(softwareRecordId))
        {
            document.SoftwareRecordId = softwareRecordId;
        }

        db.SoftwareSyncLogs.Add(new SoftwareSyncLog
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Direction = direction,
            Status = status,
            Detail = failReason ?? softwareRecordId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static SoftwareLookupQuery QueryFor(Document document) =>
        new(
            document.Fields?.ParcelId,
            document.Fields?.Grantor,
            document.Fields?.Grantee,
            document.Fields?.Client ?? document.Client.Name,
            document.Fields?.InstrumentDate,
            document.DeedType);

    private static bool HasKeyField(SoftwareLookupQuery query) =>
        !string.IsNullOrWhiteSpace(query.ParcelId)
        || !string.IsNullOrWhiteSpace(query.Grantor)
        || !string.IsNullOrWhiteSpace(query.Grantee)
        || !string.IsNullOrWhiteSpace(query.Client)
        || !string.IsNullOrWhiteSpace(query.InstrumentDate);

    private static SoftwareLookupResponse ToResponse(SoftwareLookupResult result) =>
        new(result.ParcelId, result.Owner, result.LegalDescription, result.Address, result.SoftwareRecordId, result.Extra);

    private static void TryMap(Dictionary<string, string> mapped, DeedTypeMap? map, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var key = field;
        if (map?.FieldMapJson is not null)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(map.FieldMapJson);
                if (doc.RootElement.TryGetProperty(field, out var mappedName) && mappedName.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    key = mappedName.GetString() ?? field;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // keep original key
            }
        }

        mapped[key] = value;
    }
}
