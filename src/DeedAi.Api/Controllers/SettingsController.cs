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

    [HttpGet("export")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> Export([FromQuery] string format, CancellationToken cancellationToken)
    {
        var flags = await db.FlagDefinitions.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        var statuses = await db.StatusDefinitions.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        var maps = await db.DeedTypeMaps.AsNoTracking().OrderBy(x => x.DeedType).ToListAsync(cancellationToken);
        var kind = (format ?? "json").ToLowerInvariant();

        if (kind is "csv" or "xlsx")
        {
            var headers = new[] { "Kind", "Name", "Code", "Color", "SoftwareCode", "FieldMapJson", "IsSystem", "IsActive" };
            var rows = flags.Select(x => (IReadOnlyList<string?>)[ "Flag", x.Name, "", x.Color, "", "", "", x.IsActive.ToString() ])
                .Concat(statuses.Select(x => (IReadOnlyList<string?>)[ "Status", x.DisplayName, x.Code, x.Color, "", "", x.IsSystem.ToString(), x.IsActive.ToString() ]))
                .Concat(maps.Select(x => (IReadOnlyList<string?>)[ "DeedType", x.DeedType, "", "", x.SoftwareCode, x.FieldMapJson, "", x.IsActive.ToString() ]))
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
            maps.Select(x => new DeedTypeItem(x.Id, x.DeedType, x.SoftwareCode, x.FieldMapJson, x.IsActive)).ToList());
        return File(
            JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true }),
            "application/json",
            "deedai-settings.json");
    }

    private static string NormalizeColor(string? color) =>
        string.IsNullOrWhiteSpace(color) ? "#374151" : color.Trim();
}
