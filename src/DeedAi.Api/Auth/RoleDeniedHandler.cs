using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace DeedAi.Api.Auth;

public sealed class RoleDeniedHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "unknown";
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            var payload = new
            {
                title = "Access denied",
                message = $"Access denied. Your {role} role cannot perform this action.",
                role
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
