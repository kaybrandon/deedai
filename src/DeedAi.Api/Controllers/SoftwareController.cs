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
        var last = (await db.SoftwareSyncLogs.AsNoTracking()
                .IgnoreQueryFilters()
                .Include(x => x.Document)
                .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var connected = !string.IsNullOrWhiteSpace(options.Value.BaseUrl)
                        || software is MockSoftwareClient;
        var connectionUrl = string.IsNullOrWhiteSpace(options.Value.BaseUrl)
                            || options.Value.BaseUrl.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
            ? null
            : options.Value.BaseUrl.Trim();
        return new SoftwareStatusResponse(
            string.IsNullOrWhiteSpace(options.Value.BaseUrl) ? "Mock" : "Http",
            connected,
            policy.SoftwarePushEnabled,
            policy.SoftwareDefaultGroup,
            last?.CreatedAt ?? last?.Document?.LastSoftwareSyncAt,
            last?.Status ?? last?.Document?.LastSoftwareSyncStatus,
            last is { Status: "Failed" or "Miss" } ? last.Detail : last?.Document?.LastSoftwareSyncFailReason,
            last?.DocumentId,
            last?.Document?.Name,
            KeyConfigured(),
            connectionUrl);
    }

    [HttpGet("software/settings")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SoftwareSettingsResponse>> Settings(CancellationToken cancellationToken)
    {
        var policy = await SoftwarePushMapper.EnsurePolicyAsync(db, cancellationToken);
        return await ToSettingsAsync(policy, cancellationToken);
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
        return await ToSettingsAsync(policy, cancellationToken);
    }

    [HttpGet("software/client-configs")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<ActionResult<IReadOnlyList<SoftwareClientConfigItem>>> ClientConfigs(
        CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        return await LoadClientConfigsAsync(allowed, cancellationToken);
    }

    [HttpPut("software/client-config/{clientId:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SoftwareClientConfigItem>> UpdateClientConfig(
        Guid clientId,
        [FromBody] UpdateSoftwareClientConfigRequest request,
        CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientId, cancellationToken);
        if (client is null)
        {
            return NotFound(new { message = "Client not found." });
        }

        if (request.DateLabelDepth is < 1 or > 3)
        {
            return BadRequest(new { message = "Mapped date/label depth must be 1, 2, or 3." });
        }

        if (request.ConsiderationThreshold < 0)
        {
            return BadRequest(new { message = "Consideration threshold cannot be negative." });
        }

        if (request.GranteeCombiner is { } combiner && !GranteeCombiners.IsKnown(combiner))
        {
            return BadRequest(new { message = "Grantee combiner must be first, last, and, ampersand, semicolon, or comma." });
        }

        if (!SoftwareYears.IsValid(request.CertifiedYear, out var certifiedError))
        {
            return BadRequest(new { message = certifiedError });
        }

        if (!SoftwareYears.IsValid(request.DefaultYear, out var defaultYearError))
        {
            return BadRequest(new { message = defaultYearError });
        }

        var item = await db.SoftwareClientConfigs.FirstOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);
        if (item is null)
        {
            item = new SoftwareClientConfig { Id = Guid.NewGuid(), ClientId = clientId };
            db.SoftwareClientConfigs.Add(item);
        }

        item.Vendor = TrimOrNull(request.Vendor, 64);
        item.ApiUrl = TrimOrNull(request.ApiUrl, 256);
        item.GroupCode = TrimOrNull(request.GroupCode, 32);
        item.RemoveLeadingZeros = request.RemoveLeadingZeros;
        item.DateLabelDepth = request.DateLabelDepth;
        item.DisplaySalesTab = request.DisplaySalesTab;
        item.SendConsideration = request.SendConsideration;
        item.ConsiderationThreshold = request.ConsiderationThreshold;
        item.ResetExemptions = request.ResetExemptions;
        item.ResetSupplementYear = request.ResetSupplementYear;
        item.ResetSalesLetter = request.ResetSalesLetter;
        item.ResetSalesTab = request.ResetSalesTab;
        item.ResetAgents = request.ResetAgents;
        item.ResetMortgageCodes = request.ResetMortgageCodes;
        item.GranteeCombiner = GranteeCombiners.Normalize(request.GranteeCombiner);
        item.CertifiedYear = request.CertifiedYear;
        item.DefaultYear = request.DefaultYear;
        item.LookupImageCode = TrimOrEmpty(request.LookupImageCode, 32);
        item.PushImageCode = TrimOrEmpty(request.PushImageCode, 32);
        item.SalesRatioCode = TrimOrEmpty(request.SalesRatioCode, 32);
        item.FinanceCode = TrimOrEmpty(request.FinanceCode, 32);
        item.InstrumentCode = TrimOrEmpty(request.InstrumentCode, 32);
        item.CoalesceNullDepthFields();
        await db.SaveChangesAsync(cancellationToken);
        var imageCodes = await LoadImageCodesAsync([clientId], cancellationToken);
        return ToConfig(item, client.Name, imageCodes);
    }

    [HttpGet("software/image-codes")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<ActionResult<IReadOnlyList<SoftwareImageCodeItem>>> ImageCodes(CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        return await LoadImageCodesAsync(allowed, cancellationToken);
    }

    [HttpPost("software/image-codes")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SoftwareImageCodeItem>> CreateImageCode(
        [FromBody] UpsertSoftwareImageCodeRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateImageCode(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        if (!await db.Clients.AnyAsync(x => x.Id == request.ClientId, cancellationToken))
        {
            return BadRequest(new { message = "Client not found." });
        }

        var item = new SoftwareImageCode
        {
            Id = Guid.NewGuid(),
            ClientId = request.ClientId,
            Code = request.Code.Trim().ToUpperInvariant(),
            Label = request.Label.Trim(),
            DeedType = string.IsNullOrWhiteSpace(request.DeedType) ? "" : request.DeedType.Trim(),
            UseOnLookup = request.UseOnLookup,
            UseOnPush = request.UseOnPush,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };
        db.SoftwareImageCodes.Add(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "An image code with that value already exists for this Client." });
        }

        return ToImageCode(await ReloadImageCode(item.Id, cancellationToken));
    }

    [HttpPut("software/image-codes/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SoftwareImageCodeItem>> UpdateImageCode(
        Guid id,
        [FromBody] UpsertSoftwareImageCodeRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateImageCode(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var item = await db.SoftwareImageCodes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.ClientId = request.ClientId;
        item.Code = request.Code.Trim().ToUpperInvariant();
        item.Label = request.Label.Trim();
        item.DeedType = string.IsNullOrWhiteSpace(request.DeedType) ? "" : request.DeedType.Trim();
        item.UseOnLookup = request.UseOnLookup;
        item.UseOnPush = request.UseOnPush;
        item.IsActive = request.IsActive;
        item.SortOrder = request.SortOrder;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "An image code with that value already exists for this Client." });
        }

        return ToImageCode(await ReloadImageCode(id, cancellationToken));
    }

    [HttpDelete("software/image-codes/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> DeleteImageCode(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.SoftwareImageCodes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        db.SoftwareImageCodes.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Image code removed." });
    }

    [HttpGet("software/sales-tab-codes")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<ActionResult<IReadOnlyList<SalesTabCodeItem>>> SalesTabCodes(CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        return await LoadSalesCodesAsync(allowed, cancellationToken);
    }

    [HttpPost("software/sales-tab-codes")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SalesTabCodeItem>> CreateSalesTabCode(
        [FromBody] UpsertSalesTabCodeRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateSalesCode(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        if (request.ClientId is { } clientId
            && !await db.Clients.AnyAsync(x => x.Id == clientId, cancellationToken))
        {
            return BadRequest(new { message = "Client not found." });
        }

        var item = new SalesTabCode
        {
            Id = Guid.NewGuid(),
            ClientId = request.ClientId,
            Code = request.Code.Trim().ToUpperInvariant(),
            Label = request.Label.Trim(),
            MinConsideration = request.MinConsideration,
            MaxConsideration = request.MaxConsideration,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };
        db.SalesTabCodes.Add(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "A Sales Tab code with that value already exists for this Client." });
        }

        return ToSalesCode(await ReloadSalesCode(item.Id, cancellationToken));
    }

    [HttpPut("software/sales-tab-codes/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SalesTabCodeItem>> UpdateSalesTabCode(
        Guid id,
        [FromBody] UpsertSalesTabCodeRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateSalesCode(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var item = await db.SalesTabCodes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.ClientId = request.ClientId;
        item.Code = request.Code.Trim().ToUpperInvariant();
        item.Label = request.Label.Trim();
        item.MinConsideration = request.MinConsideration;
        item.MaxConsideration = request.MaxConsideration;
        item.IsActive = request.IsActive;
        item.SortOrder = request.SortOrder;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { message = "A Sales Tab code with that value already exists for this Client." });
        }

        return ToSalesCode(await ReloadSalesCode(id, cancellationToken));
    }

    [HttpDelete("software/sales-tab-codes/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> DeleteSalesTabCode(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.SalesTabCodes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        db.SalesTabCodes.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Sales Tab code removed." });
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
        [FromQuery] int? year,
        [FromQuery] string? imageCode,
        CancellationToken cancellationToken)
    {
        var query = await EnrichLookupAsync(
            new SoftwareLookupQuery(parcelId, grantor, grantee, client, instrumentDate, deedType, year, imageCode),
            clientId: null,
            deedType,
            cancellationToken);
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

        var query = await QueryForAsync(document, cancellationToken);
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

        var clientConfig = await db.SoftwareClientConfigs
            .FirstOrDefaultAsync(x => x.ClientId == document.ClientId, cancellationToken);
        var salesCodes = await db.SalesTabCodes.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var values = SoftwarePushMapper.ResolveFieldValues(document, policy, clientConfig);
        var mapped = await SoftwarePushMapper.BuildMappedFieldsAsync(
            db, document, map, policy, clientConfig, salesCodes, cancellationToken);
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

    private static bool HasKeyField(SoftwareLookupQuery query) =>
        !string.IsNullOrWhiteSpace(query.ParcelId)
        || !string.IsNullOrWhiteSpace(query.Grantor)
        || !string.IsNullOrWhiteSpace(query.Grantee)
        || !string.IsNullOrWhiteSpace(query.Client)
        || !string.IsNullOrWhiteSpace(query.InstrumentDate);

    private static SoftwareLookupResponse ToResponse(SoftwareLookupResult result) =>
        new(result.ParcelId, result.Owner, result.LegalDescription, result.Address, result.SoftwareRecordId, result.Extra);

    private async Task<SoftwareSettingsResponse> ToSettingsAsync(AppPolicy policy, CancellationToken cancellationToken) =>
        new(
            policy.SoftwarePushEnabled,
            policy.SoftwareDefaultGroup,
            policy.SoftwareFieldDefaultsJson,
            KeyConfigured(),
            await LoadClientConfigsAsync(null, cancellationToken),
            await LoadSalesCodesAsync(null, cancellationToken),
            await LoadImageCodesAsync(null, cancellationToken));

    private bool KeyConfigured() => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    private async Task<List<SoftwareClientConfigItem>> LoadClientConfigsAsync(
        IReadOnlyList<Guid>? allowed,
        CancellationToken cancellationToken)
    {
        var clients = await ClientAccess.VisibleClients(db.Clients.AsNoTracking(), allowed)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var configs = await db.SoftwareClientConfigs.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var config in configs)
        {
            config.CoalesceNullDepthFields();
        }

        var imageCodes = await LoadImageCodesAsync(allowed, cancellationToken);
        return clients
            .Select(client =>
            {
                var config = configs.FirstOrDefault(x => x.ClientId == client.Id);
                var scoped = imageCodes.Where(x => x.ClientId == client.Id).ToList();
                return config is null ? EmptyConfig(client, scoped) : ToConfig(config, client.Name, scoped);
            })
            .ToList();
    }

    private async Task<List<SalesTabCodeItem>> LoadSalesCodesAsync(
        IReadOnlyList<Guid>? allowed,
        CancellationToken cancellationToken)
    {
        var rows = await db.SalesTabCodes.AsNoTracking()
            .Include(x => x.Client)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);
        return rows
            .Where(x => x.ClientId is null || ClientAccess.CanSee(allowed, x.ClientId.Value))
            .Select(ToSalesCode)
            .ToList();
    }

    private async Task<SalesTabCode> ReloadSalesCode(Guid id, CancellationToken cancellationToken) =>
        await db.SalesTabCodes.AsNoTracking().Include(x => x.Client).FirstAsync(x => x.Id == id, cancellationToken);

    private static SoftwareClientConfigItem EmptyConfig(Client client, IReadOnlyList<SoftwareImageCodeItem>? imageCodes = null) =>
        new(client.Id, client.Name, null, null, null, false, 1, false, true, 0,
            false, false, false, false, false, false, false,
            GranteeCombiners.First, null, null, "", "", "", "", "",
            imageCodes ?? []);

    private static SoftwareClientConfigItem ToConfig(
        SoftwareClientConfig item,
        string clientName,
        IReadOnlyList<SoftwareImageCodeItem>? imageCodes = null)
    {
        item.CoalesceNullDepthFields();
        return new(
            item.ClientId,
            clientName,
            item.Vendor,
            item.ApiUrl,
            item.GroupCode,
            item.RemoveLeadingZeros,
            item.DateLabelDepth,
            item.DisplaySalesTab,
            item.SendConsideration,
            item.ConsiderationThreshold,
            item.ResetExemptions,
            item.ResetSupplementYear,
            item.ResetSalesLetter,
            item.ResetSalesTab,
            item.ResetAgents,
            item.ResetMortgageCodes,
            item.HasAnyReset,
            item.GranteeCombiner,
            item.CertifiedYear,
            item.DefaultYear,
            item.LookupImageCode,
            item.PushImageCode,
            item.SalesRatioCode,
            item.FinanceCode,
            item.InstrumentCode,
            imageCodes ?? []);
    }

    private static SalesTabCodeItem ToSalesCode(SalesTabCode item) =>
        new(item.Id, item.ClientId, item.Client?.Name, item.Code, item.Label,
            item.MinConsideration, item.MaxConsideration, item.IsActive, item.SortOrder);

    private static string? TrimOrNull(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static string TrimOrEmpty(string? value, int max) =>
        TrimOrNull(value, max) ?? "";

    private async Task<List<SoftwareImageCodeItem>> LoadImageCodesAsync(
        IReadOnlyList<Guid>? allowed,
        CancellationToken cancellationToken)
    {
        var rows = await db.SoftwareImageCodes.AsNoTracking()
            .Include(x => x.Client)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.CoalesceNullFields();
        }

        return rows
            .Where(x => ClientAccess.CanSee(allowed, x.ClientId))
            .Select(ToImageCode)
            .ToList();
    }

    private async Task<SoftwareImageCode> ReloadImageCode(Guid id, CancellationToken cancellationToken) =>
        await db.SoftwareImageCodes.AsNoTracking().Include(x => x.Client).FirstAsync(x => x.Id == id, cancellationToken);

    private static SoftwareImageCodeItem ToImageCode(SoftwareImageCode item)
    {
        item.CoalesceNullFields();
        return new(
            item.Id,
            item.ClientId,
            item.Client?.Name,
            item.Code,
            item.Label,
            string.IsNullOrWhiteSpace(item.DeedType) ? null : item.DeedType,
            item.UseOnLookup,
            item.UseOnPush,
            item.IsActive,
            item.SortOrder);
    }

    private static string? ValidateImageCode(UpsertSoftwareImageCodeRequest request)
    {
        if (request.ClientId == Guid.Empty)
        {
            return "Image codes are Client-scoped.";
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "Image code is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return "Image code label is required.";
        }

        return null;
    }

    private async Task<SoftwareLookupQuery> QueryForAsync(Document document, CancellationToken cancellationToken)
    {
        var config = await db.SoftwareClientConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClientId == document.ClientId, cancellationToken);
        config?.CoalesceNullDepthFields();
        var codes = await db.SoftwareImageCodes.AsNoTracking()
            .Where(x => x.ClientId == document.ClientId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        return new SoftwareLookupQuery(
            document.EffectivePid,
            PartyNames.Primary(document.Grantors, document.Fields?.Grantor),
            GranteeCombiners.Combine(document.Grantees, document.Fields?.Grantee, config?.GranteeCombiner),
            document.Fields?.Client ?? document.Client.Name,
            document.Fields?.InstrumentDate,
            document.DeedType,
            SoftwareYears.Prefer(config?.DefaultYear, config?.CertifiedYear),
            SoftwareImageCodes.ForLookup(config, codes, document.DeedType));
    }

    private async Task<SoftwareLookupQuery> EnrichLookupAsync(
        SoftwareLookupQuery query,
        Guid? clientId,
        string? deedType,
        CancellationToken cancellationToken)
    {
        SoftwareClientConfig? config = null;
        if (clientId is { } id)
        {
            config = await db.SoftwareClientConfigs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ClientId == id, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(query.Client))
        {
            var client = await db.Clients.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name == query.Client, cancellationToken);
            if (client is not null)
            {
                config = await db.SoftwareClientConfigs.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ClientId == client.Id, cancellationToken);
                clientId = client.Id;
            }
        }

        config?.CoalesceNullDepthFields();
        var codes = clientId is { } scoped
            ? await db.SoftwareImageCodes.AsNoTracking().Where(x => x.ClientId == scoped).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken)
            : [];
        return query with
        {
            Year = query.Year ?? SoftwareYears.Prefer(config?.DefaultYear, config?.CertifiedYear),
            ImageCode = string.IsNullOrWhiteSpace(query.ImageCode)
                ? SoftwareImageCodes.ForLookup(config, codes, deedType ?? query.DeedType)
                : query.ImageCode.Trim(),
            Grantee = string.IsNullOrWhiteSpace(query.Grantee)
                ? query.Grantee
                : query.Grantee
        };
    }

    private static string? ValidateSalesCode(UpsertSalesTabCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "Sales Tab code is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return "Sales Tab label is required.";
        }

        if (request.MinConsideration < 0 || (request.MaxConsideration is { } max && max < request.MinConsideration))
        {
            return "Consideration range is invalid.";
        }

        return null;
    }

    private async Task<SoftwareFieldMap> ReloadMap(Guid id, CancellationToken cancellationToken) =>
        await db.SoftwareFieldMaps.AsNoTracking().Include(x => x.Client).FirstAsync(x => x.Id == id, cancellationToken);

    private static SoftwareFieldMapItem ToMap(SoftwareFieldMap item) =>
        new(item.Id, item.DeedField, item.SoftwareField, item.SoftwareGroup, item.ClientId, item.Client?.Name,
            item.DeedType, item.IsActive, item.SortOrder);

    private static string? ValidateMap(UpsertSoftwareFieldMapRequest request)
    {
        if (!DeedFields.IsKnown(request.DeedField))
        {
            return "Deed field must be a known Software map key (grantor, grantee, mailing, volume, page, documentNumber, years, imageCode, and the other typed deed fields).";
        }

        if (string.IsNullOrWhiteSpace(request.SoftwareField))
        {
            return "Software field is required.";
        }

        return null;
    }
}
