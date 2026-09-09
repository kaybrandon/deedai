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
        [FromQuery] string parcelId,
        [FromQuery] string? client,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parcelId))
        {
            return BadRequest(new { message = "Parcel ID is required." });
        }

        var result = await software.LookupAsync(parcelId.Trim(), client, cancellationToken);
        if (result is null)
        {
            return NotFound(new { message = "No Software record found for that parcel." });
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

        var parcel = document.Fields?.ParcelId;
        if (string.IsNullOrWhiteSpace(parcel))
        {
            return BadRequest(new { message = "Add a Parcel ID before looking up Software." });
        }

        var result = await software.LookupAsync(parcel, document.Client.Name, cancellationToken);
        db.SoftwareSyncLogs.Add(new SoftwareSyncLog
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Direction = "Lookup",
            Status = result is null ? "Miss" : "Ok",
            Detail = result is null ? "No Software record." : result.SoftwareRecordId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        if (result is null)
        {
            return NotFound(new { message = "No Software record found for that parcel." });
        }

        return ToResponse(result);
    }

    [HttpPost("documents/{id:guid}/software/push")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<ActionResult<SoftwarePushResponse>> Push(Guid id, CancellationToken cancellationToken)
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

        db.SoftwareSyncLogs.Add(new SoftwareSyncLog
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Direction = "Push",
            Status = result.Succeeded ? "Ok" : "Failed",
            Detail = result.Message,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return new SoftwarePushResponse(result.Succeeded, result.SoftwareRecordId, result.Message);
    }

    private async Task<Document?> LoadVisible(Guid id, CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        return await ClientAccess.VisibleDocuments(db.Documents, allowed)
            .Include(x => x.Client)
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

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
