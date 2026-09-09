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
[Route("api/auth")]
public sealed class AuthController(
    DeedAiDbContext db,
    JwtTokenService tokens,
    IEmailOutbound email,
    EmailOutbound outbound,
    IConfiguration configuration,
    IBlobStorage blobs) : ControllerBase
{
    public const int ResetHours = 1;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var emailAddress = request.Email.Trim();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == emailAddress, cancellationToken);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new
            {
                title = "Sign in failed",
                message = "Email or password is incorrect."
            });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new
            {
                title = "Sign in failed",
                message = "This account is disabled. Contact an Admin."
            });
        }

        var settings = await outbound.EnsureAsync(cancellationToken);
        if (settings.VerifyRequired && !user.EmailVerified)
        {
            return Unauthorized(new
            {
                title = "Sign in failed",
                message = "Verify your email before signing in. Contact an Admin if you need a new link."
            });
        }

        return new LoginResponse(tokens.Create(user), user.Email, user.DisplayName, user.Role);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = ClientAccess.UserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await db.Users
            .Include(x => x.ClientAccess)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return await ToMe(user, cancellationToken);
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ClientAccess.UserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await db.Users.Include(x => x.ClientAccess).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { message = "Display name is required.", field = "displayName" });
        }

        if (PasswordRules.Validate(request.Password, required: false) is { } passwordError)
        {
            return BadRequest(new
            {
                message = passwordError,
                field = "password",
                errors = new Dictionary<string, string[]> { ["password"] = [passwordError] }
            });
        }

        user.DisplayName = request.DisplayName.Trim();
        user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = PasswordHasher.Hash(request.Password);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ToMe(user, cancellationToken);
    }

    [HttpPost("me/photo")]
    [Authorize]
    [RequestSizeLimit(ProfilePhotos.MaxBytes + (256 * 1024))]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfilePhotos.MaxBytes + (256 * 1024))]
    public async Task<ActionResult<MeResponse>> UploadMyPhoto(IFormFile? file, CancellationToken cancellationToken)
    {
        var user = await LoadSelf(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        if (ProfilePhotos.Validate(file) is { } error)
        {
            return BadRequest(new { message = error, field = "photo" });
        }

        await ProfilePhotos.SaveAsync(blobs, user, file!, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToMe(user, cancellationToken);
    }

    [HttpDelete("me/photo")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> ClearMyPhoto(CancellationToken cancellationToken)
    {
        var user = await LoadSelf(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        await ProfilePhotos.ClearAsync(blobs, user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ToMe(user, cancellationToken);
    }

    private async Task<UserAccount?> LoadSelf(CancellationToken cancellationToken)
    {
        var userId = ClientAccess.UserId(User);
        if (userId is null)
        {
            return null;
        }

        return await db.Users.Include(x => x.ClientAccess).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    private async Task<MeResponse> ToMe(UserAccount user, CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var clients = await ClientAccess.VisibleClients(db.Clients.AsNoTracking().Where(x => x.IsActive), allowed)
            .OrderBy(x => x.Name)
            .Select(x => new ClientScopeItem(x.Id, x.Name))
            .ToListAsync(cancellationToken);

        return new MeResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.FullName,
            user.Role,
            user.ClientAccess.Select(x => x.ClientId).ToList(),
            clients,
            !string.IsNullOrWhiteSpace(user.PhotoBlobPath));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var emailAddress = request.Email.Trim();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == emailAddress && x.IsActive, cancellationToken);
        if (user is not null)
        {
            var raw = TokenHasher.NewToken();
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = TokenHasher.Hash(raw),
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(ResetHours)
            });
            await db.SaveChangesAsync(cancellationToken);

            var publicUrl = DependencyInjection.FirstValue(configuration, "AppPublicUrl", "App:PublicUrl")
                            ?? $"{Request.Scheme}://{Request.Host.Value}";
            var link = $"{publicUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(raw)}";
            try
            {
                await email.SendAsync(
                    new EmailMessage(
                        user.Email,
                        "Reset your Deed AI password",
                        $"Reset your Deed AI password using this link (expires in {ResetHours} hour):\n{link}\nReset token: {raw}",
                        $"<p>Reset your Deed AI password using this link (expires in {ResetHours} hour):</p><p><a href=\"{link}\">{link}</a></p>"),
                    cancellationToken);
            }
            catch (EmailNotConfiguredException)
            {
                // Fail closed — do not leak whether the account exists.
            }
            catch (InvalidOperationException)
            {
                // Fail closed — same generic response.
            }
        }

        return Ok(new
        {
            message = "If that email is on file, we sent a reset link."
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { title = "Reset failed", message = "Token and password are required." });
        }

        if (PasswordRules.Validate(request.Password) is { } passwordError)
        {
            return BadRequest(new
            {
                title = "Reset failed",
                message = passwordError,
                field = "password",
                errors = new { password = new[] { passwordError } }
            });
        }

        var hash = TokenHasher.Hash(request.Token.Trim());
        var token = await db.PasswordResetTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (token is null || token.UsedAt is not null || token.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return BadRequest(new
            {
                title = "Reset failed",
                message = "This reset link is invalid or has expired."
            });
        }

        token.UsedAt = DateTimeOffset.UtcNow;
        token.User.PasswordHash = PasswordHasher.Hash(request.Password);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Password updated. You can sign in now." });
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { title = "Verify failed", message = "A verification token is required." });
        }

        var hash = TokenHasher.Hash(request.Token.Trim());
        var token = await db.EmailVerificationTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (token is null || token.UsedAt is not null || token.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return BadRequest(new
            {
                title = "Verify failed",
                message = "This verification link is invalid or has expired."
            });
        }

        token.UsedAt = DateTimeOffset.UtcNow;
        token.User.EmailVerified = true;
        await db.SaveChangesAsync(cancellationToken);

        if (!token.User.IsActive)
        {
            return Ok(new
            {
                message = "Email verified. This account is disabled, so you still cannot sign in."
            });
        }

        return Ok(new { message = "Email verified. You can sign in now." });
    }
}
