using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Software;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class SoftwareController(
    DeedAiDbContext db,
    ISoftwareClient software,
    IOptions<SoftwareOptions> options) : ControllerBase
{
    [HttpGet("software/status")]
    public async Task<ActionResult<SoftwareStatusResponse>> Status(CancellationToken cancellationToken)
    {
        var policy = await SoftwarePushMapper.EnsurePolicyAsync(db, cancellationToken);
        var last = await db.SoftwareSyncLogs.AsNoTracking()
            .Include(x => x.Document)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var connected = !string.IsNullOrWhiteSpace(options.Value.BaseUrl)
                        || software is MockSoftwareClient;
        return new SoftwareStatusResponse(
            string.IsNullOrWhiteSpace(options.Value.BaseUrl) ? "Mock" : "Http",
            connected,
            policy.SoftwarePushEnabled,
            policy.SoftwareDefaultGroup,
            last?.CreatedAt ?? last?.Document.LastSoftwareSyncAt,
            last?.Status ?? last?.Document.LastSoftwareSyncStatus,
            last is { Status: "Failed" or "Miss" } ? last.Detail : last?.Document.LastSoftwareSyncFailReason,
            last?.DocumentId,
            last?.Document.Name);
    }

    [HttpGet("software/settings")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SoftwareSettingsResponse>> Settings(CancellationToken cancellationToken)
    {
        var policy = await SoftwarePushMapper.EnsurePolicyAsync(db, cancellationToken);
        return ToSettings(policy);
    }

    [HttpPut("software/settings")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SoftwareSettingsResponse>> UpdateSettings(
        [FromBody] UpdateSoftwareSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var policy = await SoftwarePushMapper.EnsurePolicyAsync(db, cancellationToken);
        policy.SoftwarePushEnabled = request.PushEnabled;
        policy.SoftwareDefaultGroup = string.IsNullOrWhiteSpace(request.DefaultGroup) ? null : request.DefaultGroup.Trim();
        policy.SoftwareFieldDefaultsJson = string.IsNullOrWhiteSpace(request.FieldDefaultsJson) ? null : request.FieldDefaultsJson.Trim();
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToSettings(policy);
    }

    [HttpGet("software/field-maps")]
    public async Task<ActionResult<IReadOnlyList<SoftwareFieldMapItem>>> FieldMaps(CancellationToken cancellationToken)
    {
        var rows = await db.SoftwareFieldMaps.AsNoTracking()
            .Include(x => x.Client)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DeedField)
            .ToListAsync(cancellationToken);
        return rows.Select(ToMap).ToList();
    }

    [HttpPost("software/field-maps")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SoftwareFieldMapItem>> CreateFieldMap(
        [FromBody] UpsertSoftwareFieldMapRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateMap(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var item = new SoftwareFieldMap
        {
            Id = Guid.NewGuid(),
            DeedField = request.DeedField.Trim(),
            SoftwareField = request.SoftwareField.Trim(),
            SoftwareGroup = string.IsNullOrWhiteSpace(request.SoftwareGroup) ? null : request.SoftwareGroup.Trim(),
            ClientId = request.ClientId,
            DeedType = string.IsNullOrWhiteSpace(request.DeedType) ? null : request.DeedType.Trim(),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };
        db.SoftwareFieldMaps.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return ToMap(await ReloadMap(item.Id, cancellationToken));
    }

    [HttpPut("software/field-maps/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SoftwareFieldMapItem>> UpdateFieldMap(
        Guid id,
        [FromBody] UpsertSoftwareFieldMapRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateMap(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var item = await db.SoftwareFieldMaps.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.DeedField = request.DeedField.Trim();
        item.SoftwareField = request.SoftwareField.Trim();
        item.SoftwareGroup = string.IsNullOrWhiteSpace(request.SoftwareGroup) ? null : request.SoftwareGroup.Trim();
        item.ClientId = request.ClientId;
        item.DeedType = string.IsNullOrWhiteSpace(request.DeedType) ? null : request.DeedType.Trim();
        item.IsActive = request.IsActive;
        item.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(cancellationToken);
        return ToMap(await ReloadMap(id, cancellationToken));
    }

    [HttpDelete("software/field-maps/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> DeleteFieldMap(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.SoftwareFieldMaps.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        db.SoftwareFieldMaps.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Field map removed." });
    }

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

        var policy = await SoftwarePushMapper.EnsurePolicyAsync(db, cancellationToken);
        if (!policy.SoftwarePushEnabled)
        {
            return BadRequest(new { message = "Software push is disabled. An Admin can enable it on the Software page or Settings." });
        }

        var map = document.DeedType is null
            ? null
            : await db.DeedTypeMaps.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DeedType == document.DeedType && x.IsActive, cancellationToken);

        var values = SoftwarePushMapper.ResolveFieldValues(document, policy);
        var defaults = await db.PropertyDefaults.AsNoTracking().ToListAsync(cancellationToken);
        SoftwarePushMapper.ApplyPropertyDefaults(values, defaults, document.ClientId, document.DeedType);
        var mapped = await SoftwarePushMapper.BuildMappedFieldsAsync(db, document, map, policy, cancellationToken);
        foreach (var pair in values.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
        {
            mapped.TryAdd(pair.Key, pair.Value!);
        }

        var result = await software.PushAsync(
            new SoftwarePushRequest(
                document.Id,
                values.GetValueOrDefault(DeedFields.ParcelId),
                document.DeedType,
                map?.SoftwareCode,
                values.GetValueOrDefault(DeedFields.Grantor),
                values.GetValueOrDefault(DeedFields.Grantee),
                values.GetValueOrDefault(DeedFields.InstrumentDate),
                values.GetValueOrDefault(DeedFields.Consideration),
                values.GetValueOrDefault(DeedFields.Client),
                values.GetValueOrDefault(DeedFields.Notes),
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

    private static SoftwareSettingsResponse ToSettings(AppPolicy policy) =>
        new(policy.SoftwarePushEnabled, policy.SoftwareDefaultGroup, policy.SoftwareFieldDefaultsJson);

    private async Task<SoftwareFieldMap> ReloadMap(Guid id, CancellationToken cancellationToken) =>
        await db.SoftwareFieldMaps.AsNoTracking().Include(x => x.Client).FirstAsync(x => x.Id == id, cancellationToken);

    private static SoftwareFieldMapItem ToMap(SoftwareFieldMap item) =>
        new(item.Id, item.DeedField, item.SoftwareField, item.SoftwareGroup, item.ClientId, item.Client?.Name,
            item.DeedType, item.IsActive, item.SortOrder);

    private static string? ValidateMap(UpsertSoftwareFieldMapRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeedField) || !DeedFields.All.Contains(request.DeedField.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return "Deed field must be grantor, grantee, instrumentDate, consideration, parcelId, client, or notes.";
        }

        if (string.IsNullOrWhiteSpace(request.SoftwareField))
        {
            return "Software field is required.";
        }

        return null;
    }
}
