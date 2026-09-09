using DeedAi.Api.Auth;
using DeedAi.Api.Contracts;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure;
using DeedAi.Infrastructure.Data;
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
    IEmailSender email,
    IConfiguration configuration) : ControllerBase
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

        return new MeResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role,
            user.ClientAccess.Select(x => x.ClientId).ToList());
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
            await email.SendAsync(
                new EmailMessage(
                    user.Email,
                    "Reset your Deed AI password",
                    $"Reset your Deed AI password using this link (expires in {ResetHours} hour):\n{link}\nReset token: {raw}",
                    $"<p>Reset your Deed AI password using this link (expires in {ResetHours} hour):</p><p><a href=\"{link}\">{link}</a></p>"),
                cancellationToken);
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
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { title = "Reset failed", message = "Token and password are required." });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new { title = "Reset failed", message = "Password must be at least 8 characters." });
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
}
