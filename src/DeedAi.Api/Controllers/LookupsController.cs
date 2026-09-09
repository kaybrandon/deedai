using DeedAi.Api.Auth;
using DeedAi.Api.Contracts;
using DeedAi.Domain.Abstractions;
using DeedAi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class LookupsController(DeedAiDbContext db, IBlobStorage blobs) : ControllerBase
{
    [HttpGet("clients")]
    public async Task<ActionResult<IReadOnlyList<ClientResponse>>> Clients(CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var items = await ClientAccess.VisibleClients(db.Clients.AsNoTracking(), allowed)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new ClientResponse(x.Id, x.Name, x.IsActive))
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

    [HttpGet("users/{id:guid}/photo")]
    public async Task<IActionResult> Photo(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var file = await ProfilePhotos.OpenAsync(blobs, user, cancellationToken);
        return file ?? NotFound();
    }

}
