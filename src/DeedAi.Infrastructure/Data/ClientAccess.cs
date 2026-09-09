using System.Security.Claims;
using DeedAi.Domain;
using DeedAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Infrastructure.Data;

public static class ClientAccess
{
    public static Guid? UserId(ClaimsPrincipal user)
    {
        var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var userId) ? userId : null;
    }

    public static string Role(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Role)?.Value ?? AppRoles.Viewer;

    public static async Task<IReadOnlyList<Guid>?> AllowedClientIdsAsync(
        DeedAiDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (AppRoles.CanAdmin(Role(user)))
        {
            return null;
        }

        var userId = UserId(user);
        if (userId is null)
        {
            return [];
        }

        return await db.UserClientAccess
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.ClientId)
            .ToListAsync(cancellationToken);
    }

    public static IQueryable<Document> VisibleDocuments(
        IQueryable<Document> query,
        IReadOnlyList<Guid>? allowedClientIds) =>
        allowedClientIds is null ? query : query.Where(x => allowedClientIds.Contains(x.ClientId));

    public static IQueryable<Client> VisibleClients(
        IQueryable<Client> query,
        IReadOnlyList<Guid>? allowedClientIds) =>
        allowedClientIds is null ? query : query.Where(x => allowedClientIds.Contains(x.Id));

    public static bool CanSee(IReadOnlyList<Guid>? allowedClientIds, Guid clientId) =>
        allowedClientIds is null || allowedClientIds.Contains(clientId);
}
