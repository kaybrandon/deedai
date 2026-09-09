using DeedAi.Api.Auth;
using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Email;
using DeedAi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize(Policy = RolePolicies.CanAdmin)]
[Route("api/admin/users")]
public sealed class UsersController(
    DeedAiDbContext db,
    IBlobStorage blobs,
    IEmailOutbound email,
    IConfiguration configuration) : ControllerBase
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
            return Invalid(error.Value);
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
            FullName = request.FullName!.Trim(),
            Role = request.Role,
            PasswordHash = PasswordHasher.Hash(request.Password!),
            IsActive = request.IsActive,
            EmailVerified = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await ReplaceAccessAsync(user.Id, request.ClientIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await TrySendVerificationAsync(user, required: false, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, ToDetail(await Reload(user.Id, cancellationToken)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDetail>> Update(Guid id, [FromBody] UpsertUserRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request, requirePassword: false);
        if (error is not null)
        {
            return Invalid(error.Value);
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

        var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        user.Email = email;
        user.DisplayName = request.DisplayName.Trim();
        user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        if (emailChanged)
        {
            user.EmailVerified = false;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = PasswordHasher.Hash(request.Password);
        }

        await ReplaceAccessAsync(user.Id, request.ClientIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (emailChanged)
        {
            await TrySendVerificationAsync(user, required: false, cancellationToken);
        }

        return ToDetail(await Reload(user.Id, cancellationToken));
    }

    [HttpPost("{id:guid}/resend-verification")]
    public async Task<IActionResult> ResendVerification(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (user.EmailVerified)
        {
            return BadRequest(new { message = "This account is already verified." });
        }

        try
        {
            await TrySendVerificationAsync(user, required: true, cancellationToken);
        }
        catch (EmailNotConfiguredException)
        {
            return BadRequest(new { message = "Email is not configured for the active mode." });
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { message = "Could not send the verification email." });
        }

        return Ok(new { message = user.IsActive
            ? "Verification email sent."
            : "Verification email sent. This account is disabled, so they still cannot sign in." });
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

    [HttpPost("{id:guid}/photo")]
    [RequestSizeLimit(ProfilePhotos.MaxBytes + (256 * 1024))]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfilePhotos.MaxBytes + (256 * 1024))]
    public async Task<ActionResult<UserDetail>> UploadPhoto(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.ClientAccess).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (ProfilePhotos.Validate(file) is { } error)
        {
            return BadRequest(new { message = error, field = "photo" });
        }

        await ProfilePhotos.SaveAsync(blobs, user, file!, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(await Reload(user.Id, cancellationToken));
    }

    [HttpDelete("{id:guid}/photo")]
    public async Task<ActionResult<UserDetail>> ClearPhoto(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(x => x.ClientAccess).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        await ProfilePhotos.ClearAsync(blobs, user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(await Reload(user.Id, cancellationToken));
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
        new(user.Id, user.Email, user.DisplayName, user.FullName, user.Role, user.IsActive, user.EmailVerified, user.CreatedAt,
            user.ClientAccess.Select(x => x.ClientId).ToList(),
            !string.IsNullOrWhiteSpace(user.PhotoBlobPath));

    private async Task TrySendVerificationAsync(UserAccount user, bool required, CancellationToken cancellationToken)
    {
        var raw = TokenHasher.NewToken();
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(raw),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(EmailOutbound.VerifyHours)
        });
        await db.SaveChangesAsync(cancellationToken);

        var publicUrl = DependencyInjection.FirstValue(configuration, "AppPublicUrl", "App:PublicUrl")
                        ?? $"{Request.Scheme}://{Request.Host.Value}";
        var link = $"{publicUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(raw)}";
        try
        {
            await email.SendAsync(
                new EmailMessage(
                    user.Email,
                    "Verify your Deed AI email",
                    $"Verify your Deed AI email using this link (expires in {EmailOutbound.VerifyHours} hours):\n{link}\nVerify token: {raw}",
                    $"<p>Verify your Deed AI email using this link (expires in {EmailOutbound.VerifyHours} hours):</p><p><a href=\"{link}\">{link}</a></p>"),
                cancellationToken);
        }
        catch (Exception) when (!required)
        {
            // User is saved; Admin can resend when mail is configured.
        }
    }

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

        if (requirePassword && string.IsNullOrWhiteSpace(request.FullName))
        {
            return new("Full name is required.", "fullName");
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
