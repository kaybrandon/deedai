using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize(Policy = RolePolicies.CanAdmin)]
[Route("api/admin/users")]
public sealed class UsersController(DeedAiDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDetail>>> List(CancellationToken cancellationToken)
    {
        var users = await db.Users
            .AsNoTracking()
            .Include(x => x.ClientAccess)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
        return users.Select(ToDetail).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.ClientAccess).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return user is null ? NotFound() : ToDetail(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserDetail>> Create([FromBody] UpsertUserRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request, requirePassword: true);
        if (error is not null)
        {
            return Invalid(error);
        }

        var email = request.Email.Trim();
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return Conflict(new { message = "A user with that email already exists." });
        }

        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role,
            PasswordHash = PasswordHasher.Hash(request.Password!),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await ReplaceAccessAsync(user.Id, request.ClientIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, ToDetail(await Reload(user.Id, cancellationToken)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDetail>> Update(Guid id, [FromBody] UpsertUserRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request, requirePassword: false);
        if (error is not null)
        {
            return Invalid(error);
        }

        var user = await db.Users.Include(x => x.ClientAccess).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var selfId = ClientAccess.UserId(User);
        if (selfId == user.Id && !request.IsActive)
        {
            return BadRequest(new { message = "You cannot disable your own account." });
        }

        if (user.Role == AppRoles.Admin && request.Role != AppRoles.Admin && !await AnotherAdminExists(user.Id, cancellationToken))
        {
            return BadRequest(new { message = "At least one Admin is required." });
        }

        var email = request.Email.Trim();
        if (await db.Users.AnyAsync(x => x.Email == email && x.Id != id, cancellationToken))
        {
            return Conflict(new { message = "A user with that email already exists." });
        }

        user.Email = email;
        user.DisplayName = request.DisplayName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = PasswordHasher.Hash(request.Password);
        }

        await ReplaceAccessAsync(user.Id, request.ClientIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(await Reload(user.Id, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (ClientAccess.UserId(User) == user.Id)
        {
            return BadRequest(new { message = "You cannot delete your own account." });
        }

        if (user.Role == AppRoles.Admin && !await AnotherAdminExists(user.Id, cancellationToken))
        {
            return BadRequest(new { message = "At least one Admin is required." });
        }

        user.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "User disabled." });
    }

    private async Task ReplaceAccessAsync(Guid userId, IReadOnlyList<Guid> clientIds, CancellationToken cancellationToken)
    {
        var existing = await db.UserClientAccess.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        db.UserClientAccess.RemoveRange(existing);
        var unique = clientIds.Distinct().ToList();
        var valid = await db.Clients.Where(x => unique.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var clientId in valid)
        {
            db.UserClientAccess.Add(new UserClientAccess { UserId = userId, ClientId = clientId });
        }
    }

    private async Task<bool> AnotherAdminExists(Guid exceptId, CancellationToken cancellationToken) =>
        await db.Users.AnyAsync(x => x.Id != exceptId && x.Role == AppRoles.Admin && x.IsActive, cancellationToken);

    private async Task<UserAccount> Reload(Guid id, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().Include(x => x.ClientAccess).FirstAsync(x => x.Id == id, cancellationToken);

    private static UserDetail ToDetail(UserAccount user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role, user.IsActive, user.CreatedAt,
            user.ClientAccess.Select(x => x.ClientId).ToList());

    private static ValidationIssue? Validate(UpsertUserRequest request, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            return new("A valid email is required.", "email");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return new("Display name is required.", "displayName");
        }

        if (!AppRoles.All.Contains(request.Role))
        {
            return new("Role must be Admin, Editor, Uploader, or Viewer.", "role");
        }

        if (PasswordRules.Validate(request.Password, requirePassword) is { } passwordError)
        {
            return new(passwordError, "password");
        }

        return null;
    }

    private static BadRequestObjectResult Invalid(ValidationIssue error) =>
        new(new
        {
            message = error.Message,
            field = error.Field,
            errors = new Dictionary<string, string[]> { [error.Field] = [error.Message] }
        });

    private readonly record struct ValidationIssue(string Message, string Field);
}
