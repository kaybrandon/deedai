using DeedAi.Api.Contracts;
using DeedAi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class LookupsController(DeedAiDbContext db) : ControllerBase
{
    [HttpGet("clients")]
    public async Task<ActionResult<IReadOnlyList<ClientResponse>>> Clients(CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var items = await ClientAccess.VisibleClients(db.Clients.AsNoTracking(), allowed)
            .OrderBy(x => x.Name)
            .Select(x => new ClientResponse(x.Id, x.Name))
            .ToListAsync(cancellationToken);
        return items;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummary>>> Users(CancellationToken cancellationToken)
    {
        var items = await db.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .Select(x => new UserSummary(x.Id, x.DisplayName, x.Role))
            .ToListAsync(cancellationToken);
        return items;
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "ok", product = "Deed AI" });
}
