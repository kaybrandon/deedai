using DeedAi.Api.Auth;
using DeedAi.Api.Contracts;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(DeedAiDbContext db, JwtTokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new
            {
                title = "Sign in failed",
                message = "Email or password is incorrect."
            });
        }

        return new LoginResponse(tokens.Create(user), user.Email, user.DisplayName, user.Role);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (id is null || !Guid.TryParse(id, out var userId))
        {
            return Unauthorized();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return new MeResponse(user.Id, user.Email, user.DisplayName, user.Role);
    }
}
