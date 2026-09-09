using System.Text.Json;
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
[Route("api/settings")]
public sealed class SettingsController(DeedAiDbContext db) : ControllerBase
{
    [HttpGet("flags")]
    public async Task<ActionResult<IReadOnlyList<FlagItem>>> Flags(CancellationToken cancellationToken) =>
        await db.FlagDefinitions.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new FlagItem(x.Id, x.Name, x.Color, x.SortOrder, x.IsActive))
            .ToListAsync(cancellationToken);

    [HttpPost("flags")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<FlagItem>> CreateFlag([FromBody] UpsertFlagRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Flag name is required." });
        }

        var item = new FlagDefinition
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Color = NormalizeColor(request.Color),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };
        db.FlagDefinitions.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return new FlagItem(item.Id, item.Name, item.Color, item.SortOrder, item.IsActive);
    }

    [HttpPut("flags/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<FlagItem>> UpdateFlag(Guid id, [FromBody] UpsertFlagRequest request, CancellationToken cancellationToken)
    {
        var item = await db.FlagDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.Name = request.Name.Trim();
        item.Color = NormalizeColor(request.Color);
        item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return new FlagItem(item.Id, item.Name, item.Color, item.SortOrder, item.IsActive);
    }

    [HttpDelete("flags/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> DeleteFlag(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.FlagDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        db.FlagDefinitions.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Flag removed." });
    }

    [HttpGet("statuses")]
    public async Task<ActionResult<IReadOnlyList<StatusItem>>> Statuses(CancellationToken cancellationToken) =>
        await db.StatusDefinitions.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.DisplayName)
            .Select(x => new StatusItem(x.Id, x.Code, x.DisplayName, x.Color, x.IsSystem, x.SortOrder, x.IsActive))
            .ToListAsync(cancellationToken);

    [HttpPost("statuses")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<StatusItem>> CreateStatus([FromBody] UpsertStatusRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { message = "Status code and display name are required." });
        }

        var item = new StatusDefinition
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Color = NormalizeColor(request.Color),
            IsSystem = false,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };
        db.StatusDefinitions.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return new StatusItem(item.Id, item.Code, item.DisplayName, item.Color, item.IsSystem, item.SortOrder, item.IsActive);
    }

    [HttpPut("statuses/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<StatusItem>> UpdateStatus(Guid id, [FromBody] UpsertStatusRequest request, CancellationToken cancellationToken)
    {
        var item = await db.StatusDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.DisplayName = request.DisplayName.Trim();
        item.Color = NormalizeColor(request.Color);
        item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive;
        if (!item.IsSystem)
        {
            item.Code = request.Code.Trim();
        }

        await db.SaveChangesAsync(cancellationToken);
        return new StatusItem(item.Id, item.Code, item.DisplayName, item.Color, item.IsSystem, item.SortOrder, item.IsActive);
    }

    [HttpDelete("statuses/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> DeleteStatus(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.StatusDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (item.IsSystem)
        {
            return BadRequest(new { message = "System pipeline statuses cannot be deleted." });
        }

        db.StatusDefinitions.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Status removed." });
    }

    [HttpGet("deed-types")]
    public async Task<ActionResult<IReadOnlyList<DeedTypeItem>>> DeedTypes(CancellationToken cancellationToken) =>
        await db.DeedTypeMaps.AsNoTracking().OrderBy(x => x.DeedType)
            .Select(x => new DeedTypeItem(x.Id, x.DeedType, x.SoftwareCode, x.FieldMapJson, x.IsActive))
            .ToListAsync(cancellationToken);

    [HttpPost("deed-types")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<DeedTypeItem>> CreateDeedType([FromBody] UpsertDeedTypeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeedType) || string.IsNullOrWhiteSpace(request.SoftwareCode))
        {
            return BadRequest(new { message = "Deed type and Software code are required." });
        }

        var item = new DeedTypeMap
        {
            Id = Guid.NewGuid(),
            DeedType = request.DeedType.Trim(),
            SoftwareCode = request.SoftwareCode.Trim(),
            FieldMapJson = request.FieldMapJson,
            IsActive = request.IsActive
        };
        db.DeedTypeMaps.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return new DeedTypeItem(item.Id, item.DeedType, item.SoftwareCode, item.FieldMapJson, item.IsActive);
    }

    [HttpPut("deed-types/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<DeedTypeItem>> UpdateDeedType(Guid id, [FromBody] UpsertDeedTypeRequest request, CancellationToken cancellationToken)
    {
        var item = await db.DeedTypeMaps.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.DeedType = request.DeedType.Trim();
        item.SoftwareCode = request.SoftwareCode.Trim();
        item.FieldMapJson = request.FieldMapJson;
        item.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return new DeedTypeItem(item.Id, item.DeedType, item.SoftwareCode, item.FieldMapJson, item.IsActive);
    }

    [HttpDelete("deed-types/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> DeleteDeedType(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.DeedTypeMaps.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        db.DeedTypeMaps.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Deed-type map removed." });
    }

    [HttpGet("clients")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<IReadOnlyList<ClientItem>>> Clients(CancellationToken cancellationToken) =>
        await db.Clients.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new ClientItem(x.Id, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);

    [HttpPost("clients")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<ClientItem>> CreateClient([FromBody] UpsertClientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Client name is required." });
        }

        var name = request.Name.Trim();
        if (await db.Clients.AnyAsync(x => x.Name == name, cancellationToken))
        {
            return Conflict(new { message = "A Client with that name already exists." });
        }

        var item = new Client { Id = Guid.NewGuid(), Name = name, IsActive = request.IsActive };
        db.Clients.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return new ClientItem(item.Id, item.Name, item.IsActive);
    }

    [HttpPut("clients/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<ClientItem>> UpdateClient(Guid id, [FromBody] UpsertClientRequest request, CancellationToken cancellationToken)
    {
        var item = await db.Clients.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Client name is required." });
        }

        var name = request.Name.Trim();
        if (await db.Clients.AnyAsync(x => x.Name == name && x.Id != id, cancellationToken))
        {
            return Conflict(new { message = "A Client with that name already exists." });
        }

        item.Name = name;
        item.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return new ClientItem(item.Id, item.Name, item.IsActive);
    }

    [HttpDelete("clients/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> DeleteClient(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.Clients.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (await db.Documents.IgnoreQueryFilters().AnyAsync(x => x.ClientId == id, cancellationToken))
        {
            item.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Client has deeds, so it was deactivated instead of deleted." });
        }

        db.Clients.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Client removed." });
    }

    [HttpGet("teams")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<IReadOnlyList<TeamItem>>> Teams(CancellationToken cancellationToken)
    {
        var teams = await db.Teams.AsNoTracking()
            .Include(x => x.Members).ThenInclude(x => x.User)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return teams.Select(ToTeam).ToList();
    }

    [HttpPost("teams")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<TeamItem>> CreateTeam([FromBody] UpsertTeamRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Team name is required." });
        }

        var name = request.Name.Trim();
        if (await db.Teams.AnyAsync(x => x.Name == name, cancellationToken))
        {
            return Conflict(new { message = "A team with that name already exists." });
        }

        var item = new Team { Id = Guid.NewGuid(), Name = name, IsActive = request.IsActive };
        db.Teams.Add(item);
        await ReplaceMembersAsync(item.Id, request.UserIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToTeam(await ReloadTeam(item.Id, cancellationToken));
    }

    [HttpPut("teams/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<TeamItem>> UpdateTeam(Guid id, [FromBody] UpsertTeamRequest request, CancellationToken cancellationToken)
    {
        var item = await db.Teams.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Team name is required." });
        }

        var name = request.Name.Trim();
        if (await db.Teams.AnyAsync(x => x.Name == name && x.Id != id, cancellationToken))
        {
            return Conflict(new { message = "A team with that name already exists." });
        }

        item.Name = name;
        item.IsActive = request.IsActive;
        await ReplaceMembersAsync(id, request.UserIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToTeam(await ReloadTeam(id, cancellationToken));
    }

    [HttpDelete("teams/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> DeleteTeam(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.Teams.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        db.Teams.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Team removed." });
    }

    [HttpGet("notifications")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<NotificationSettingsResponse>> Notifications(CancellationToken cancellationToken) =>
        ToNotification(await EnsureNotificationsAsync(cancellationToken));

    [HttpPut("notifications")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<NotificationSettingsResponse>> UpdateNotifications(
        [FromBody] UpdateNotificationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var item = await EnsureNotificationsAsync(cancellationToken);
        item.Enabled = request.Enabled;
        item.NotifyUploader = request.NotifyUploader;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToNotification(item);
    }

    [HttpGet("session")]
    public async Task<ActionResult<SessionConfigResponse>> Session(CancellationToken cancellationToken)
    {
        var item = await db.SessionSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (item is not null)
        {
            return new SessionConfigResponse(item.IdleTimeoutMinutes, SessionSettings.DefaultIdleTimeoutMinutes, "admin");
        }

        var configured = DependencyInjection.FirstValue(
            configuration,
            "SessionIdleTimeoutMinutes",
            "Session:IdleTimeoutMinutes");
        if (int.TryParse(configured, out var minutes))
        {
            return new SessionConfigResponse(SessionSettings.Clamp(minutes), SessionSettings.DefaultIdleTimeoutMinutes, "appSetting");
        }

        return new SessionConfigResponse(SessionSettings.DefaultIdleTimeoutMinutes, SessionSettings.DefaultIdleTimeoutMinutes, "default");
    }

    [HttpPut("session")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<SessionConfigResponse>> UpdateSession(
        [FromBody] UpdateSessionSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var item = await db.SessionSettings.FirstOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            item = new SessionSettings { Id = SessionSettings.SingletonId };
            db.SessionSettings.Add(item);
        }

        item.IdleTimeoutMinutes = SessionSettings.Clamp(request.IdleTimeoutMinutes);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new SessionConfigResponse(item.IdleTimeoutMinutes, SessionSettings.DefaultIdleTimeoutMinutes, "admin");
    }

    [HttpGet("ocr-cleanup")]
    public async Task<ActionResult<IReadOnlyList<OcrCleanupItem>>> OcrCleanup(CancellationToken cancellationToken) =>
        await db.OcrCleanupRules.AsNoTracking()
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Value)
            .Select(x => new OcrCleanupItem(x.Id, x.Kind, x.Value, x.IsActive, x.SortOrder))
            .ToListAsync(cancellationToken);

    [HttpPost("ocr-cleanup")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<OcrCleanupItem>> CreateOcrCleanup(
        [FromBody] UpsertOcrCleanupRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsCleanupKind(request.Kind) || string.IsNullOrWhiteSpace(request.Value))
        {
            return BadRequest(new { message = "Kind must be Trim or Discard, and a value is required. No secrets in cleanup lists." });
        }

        var value = request.Value.Trim();
        if (await db.OcrCleanupRules.AnyAsync(x => x.Kind == request.Kind && x.Value == value, cancellationToken))
        {
            return Conflict(new { message = "That cleanup rule already exists." });
        }

        var item = new OcrCleanupRule
        {
            Id = Guid.NewGuid(),
            Kind = request.Kind.Trim(),
            Value = value,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };
        db.OcrCleanupRules.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return new OcrCleanupItem(item.Id, item.Kind, item.Value, item.IsActive, item.SortOrder);
    }

    [HttpPut("ocr-cleanup/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<OcrCleanupItem>> UpdateOcrCleanup(
        Guid id,
        [FromBody] UpsertOcrCleanupRequest request,
        CancellationToken cancellationToken)
    {
        var item = await db.OcrCleanupRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        if (!IsCleanupKind(request.Kind) || string.IsNullOrWhiteSpace(request.Value))
        {
            return BadRequest(new { message = "Kind must be Trim or Discard, and a value is required." });
        }

        item.Kind = request.Kind.Trim();
        item.Value = request.Value.Trim();
        item.IsActive = request.IsActive;
        item.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(cancellationToken);
        return new OcrCleanupItem(item.Id, item.Kind, item.Value, item.IsActive, item.SortOrder);
    }

    [HttpDelete("ocr-cleanup/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> DeleteOcrCleanup(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.OcrCleanupRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        db.OcrCleanupRules.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "OCR cleanup rule removed." });
    }

    [HttpGet("property-defaults")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<IReadOnlyList<PropertyDefaultItem>>> PropertyDefaults(CancellationToken cancellationToken)
    {
        var rows = await db.PropertyDefaults.AsNoTracking()
            .Include(x => x.Client)
            .OrderBy(x => x.Scope)
            .ThenBy(x => x.FieldKey)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDefault).ToList();
    }

    [HttpPost("property-defaults")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<PropertyDefaultItem>> CreatePropertyDefault(
        [FromBody] UpsertPropertyDefaultRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateDefault(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var item = new PropertyDefault
        {
            Id = Guid.NewGuid(),
            Scope = request.Scope.Trim(),
            ClientId = request.Scope == PropertyDefaultScopes.Client ? request.ClientId : null,
            DeedType = request.Scope == PropertyDefaultScopes.DeedType ? request.DeedType?.Trim() : null,
            FieldKey = request.FieldKey.Trim(),
            DefaultValue = request.DefaultValue
        };
        db.PropertyDefaults.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return ToDefault(await ReloadDefault(item.Id, cancellationToken));
    }

    [HttpPut("property-defaults/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<ActionResult<PropertyDefaultItem>> UpdatePropertyDefault(
        Guid id,
        [FromBody] UpsertPropertyDefaultRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateDefault(request);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var item = await db.PropertyDefaults.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.Scope = request.Scope.Trim();
        item.ClientId = request.Scope == PropertyDefaultScopes.Client ? request.ClientId : null;
        item.DeedType = request.Scope == PropertyDefaultScopes.DeedType ? request.DeedType?.Trim() : null;
        item.FieldKey = request.FieldKey.Trim();
        item.DefaultValue = request.DefaultValue;
        await db.SaveChangesAsync(cancellationToken);
        return ToDefault(await ReloadDefault(id, cancellationToken));
    }

    [HttpDelete("property-defaults/{id:guid}")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> DeletePropertyDefault(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.PropertyDefaults.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        db.PropertyDefaults.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Property default removed." });
    }

    [HttpPost("property-defaults/reset")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> ResetPropertyDefaults(
        [FromBody] ResetPropertyDefaultsRequest request,
        CancellationToken cancellationToken)
    {
        if (!PropertyDefaultScopes.All.Contains(request.Scope))
        {
            return BadRequest(new { message = "Scope must be Client or DeedType." });
        }

        var query = db.PropertyDefaults.Where(x => x.Scope == request.Scope);
        if (request.Scope == PropertyDefaultScopes.Client)
        {
            if (request.ClientId is null)
            {
                return BadRequest(new { message = "Select a Client to reset." });
            }

            query = query.Where(x => x.ClientId == request.ClientId);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.DeedType))
            {
                return BadRequest(new { message = "Select a deed type to reset." });
            }

            query = query.Where(x => x.DeedType == request.DeedType);
        }

        var rows = await query.ToListAsync(cancellationToken);
        db.PropertyDefaults.RemoveRange(rows);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = $"Reset {rows.Count} property default(s).", count = rows.Count });
    }

    [HttpGet("export")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> Export([FromQuery] string format, CancellationToken cancellationToken)
    {
        var flags = await db.FlagDefinitions.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        var statuses = await db.StatusDefinitions.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        var maps = await db.DeedTypeMaps.AsNoTracking().OrderBy(x => x.DeedType).ToListAsync(cancellationToken);
        var teams = await db.Teams.AsNoTracking().Include(x => x.Members).ThenInclude(x => x.User).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var clients = await db.Clients.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var notifications = await EnsureNotificationsAsync(cancellationToken);
        var kind = (format ?? "json").ToLowerInvariant();

        if (kind is "csv" or "xlsx")
        {
            var headers = new[] { "Kind", "Name", "Code", "Color", "SoftwareCode", "FieldMapJson", "IsSystem", "IsActive" };
            var rows = flags.Select(x => (IReadOnlyList<string?>)[ "Flag", x.Name, "", x.Color, "", "", "", x.IsActive.ToString() ])
                .Concat(statuses.Select(x => (IReadOnlyList<string?>)[ "Status", x.DisplayName, x.Code, x.Color, "", "", x.IsSystem.ToString(), x.IsActive.ToString() ]))
                .Concat(maps.Select(x => (IReadOnlyList<string?>)[ "DeedType", x.DeedType, "", "", x.SoftwareCode, x.FieldMapJson, "", x.IsActive.ToString() ]))
                .Concat(teams.Select(x => (IReadOnlyList<string?>)[ "Team", x.Name, string.Join(";", x.Members.Select(m => m.User.Email)), "", "", "", "", x.IsActive.ToString() ]))
                .Concat(clients.Select(x => (IReadOnlyList<string?>)[ "Client", x.Name, "", "", "", "", "", x.IsActive.ToString() ]))
                .Concat([(IReadOnlyList<string?>)[ "Notification", "OCR Ready / Failed", notifications.NotifyUploader ? "assignee+uploader" : "assignee", "", "", "", "", notifications.Enabled.ToString() ]])
                .ToList();
            if (kind == "xlsx")
            {
                return File(SpreadsheetWriter.ToXlsx("Settings", headers, rows),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "deedai-settings.xlsx");
            }

            return File(SpreadsheetWriter.ToCsv(headers, rows), "text/csv", "deedai-settings.csv");
        }

        var payload = new SettingsExport(
            flags.Select(x => new FlagItem(x.Id, x.Name, x.Color, x.SortOrder, x.IsActive)).ToList(),
            statuses.Select(x => new StatusItem(x.Id, x.Code, x.DisplayName, x.Color, x.IsSystem, x.SortOrder, x.IsActive)).ToList(),
            maps.Select(x => new DeedTypeItem(x.Id, x.DeedType, x.SoftwareCode, x.FieldMapJson, x.IsActive)).ToList(),
            teams.Select(ToTeam).ToList(),
            clients.Select(x => new ClientItem(x.Id, x.Name, x.IsActive)).ToList(),
            ToNotification(notifications));
        return File(
            JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true }),
            "application/json",
            "deedai-settings.json");
    }

    private async Task ReplaceMembersAsync(Guid teamId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken)
    {
        var existing = await db.TeamUsers.Where(x => x.TeamId == teamId).ToListAsync(cancellationToken);
        db.TeamUsers.RemoveRange(existing);
        var unique = userIds.Distinct().ToList();
        var valid = await db.Users.Where(x => unique.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var userId in valid)
        {
            db.TeamUsers.Add(new TeamUser { TeamId = teamId, UserId = userId });
        }
    }

    private async Task<Team> ReloadTeam(Guid id, CancellationToken cancellationToken) =>
        await db.Teams.AsNoTracking().Include(x => x.Members).ThenInclude(x => x.User).FirstAsync(x => x.Id == id, cancellationToken);

    private async Task<NotificationSettings> EnsureNotificationsAsync(CancellationToken cancellationToken)
    {
        var item = await db.NotificationSettings.FirstOrDefaultAsync(cancellationToken);
        if (item is not null)
        {
            return item;
        }

        item = new NotificationSettings
        {
            Id = NotificationSettings.SingletonId,
            Enabled = true,
            NotifyUploader = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.NotificationSettings.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    private static TeamItem ToTeam(Team team) =>
        new(team.Id, team.Name, team.IsActive,
            team.Members.Select(x => new TeamMemberItem(x.UserId, x.User.DisplayName, x.User.Role, x.User.Email)).ToList());

    private static NotificationSettingsResponse ToNotification(NotificationSettings settings) =>
        new(settings.Enabled, settings.NotifyUploader, DeedAi.Infrastructure.Email.OcrNotifier.Events,
            settings.NotifyUploader
                ? "Assignee, and the uploader when that option is on."
                : "Assignee only. Turn on “also notify uploader” to include the person who uploaded the deed.");

    private static string NormalizeColor(string? color) =>
        string.IsNullOrWhiteSpace(color) ? "#374151" : color.Trim();

    private async Task<PropertyDefault> ReloadDefault(Guid id, CancellationToken cancellationToken) =>
        await db.PropertyDefaults.AsNoTracking().Include(x => x.Client).FirstAsync(x => x.Id == id, cancellationToken);

    private static PropertyDefaultItem ToDefault(PropertyDefault item) =>
        new(item.Id, item.Scope, item.ClientId, item.Client?.Name, item.DeedType, item.FieldKey, item.DefaultValue);

    private static string? ValidateDefault(UpsertPropertyDefaultRequest request)
    {
        if (!PropertyDefaultScopes.All.Contains(request.Scope))
        {
            return "Scope must be Client or DeedType.";
        }

        if (request.Scope == PropertyDefaultScopes.Client && request.ClientId is null)
        {
            return "Select a Client for this default.";
        }

        if (request.Scope == PropertyDefaultScopes.DeedType && string.IsNullOrWhiteSpace(request.DeedType))
        {
            return "Select a deed type for this default.";
        }

        if (string.IsNullOrWhiteSpace(request.FieldKey) || !DeedFields.All.Contains(request.FieldKey.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return "Field must be a deed field (grantor, grantee, parcelId, and the rest).";
        }

        return null;
    }

    private static bool IsCleanupKind(string? kind) =>
        string.Equals(kind, OcrCleanupKinds.Trim, StringComparison.OrdinalIgnoreCase)
        || string.Equals(kind, OcrCleanupKinds.Discard, StringComparison.OrdinalIgnoreCase);
}
