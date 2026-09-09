using DeedAi.Domain.Abstractions;
using DeedAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DeedAi.Infrastructure.Health;

public sealed record HealthCheckStatus(string Status, bool Reachable, string? Mode = null);

public sealed record ShallowHealth(string Status, string Product);

public sealed record DetailedHealth(
    string Status,
    string Product,
    HealthCheckStatus Sql,
    HealthCheckStatus Storage,
    HealthCheckStatus Queue);

public sealed class RuntimeHealth(
    DeedAiDbContext db,
    IBlobStorage storage,
    IOcrJobQueue queue,
    IConfiguration configuration)
{
    public const string ProductName = "Deed AI";

    public static ShallowHealth Shallow() => new("ok", ProductName);

    public async Task<DetailedHealth> DetailAsync(CancellationToken cancellationToken)
    {
        var sql = await CheckSqlAsync(cancellationToken);
        var storageCheck = await CheckStorageAsync(cancellationToken);
        var queueCheck = await CheckQueueAsync(cancellationToken);
        var overall = sql.Reachable && storageCheck.Reachable && queueCheck.Reachable ? "ok" : "degraded";
        return new DetailedHealth(overall, ProductName, sql, storageCheck, queueCheck);
    }

    private async Task<HealthCheckStatus> CheckSqlAsync(CancellationToken cancellationToken)
    {
        var mode = configuration["Database:Provider"] ?? "Sqlite";
        try
        {
            var reachable = await db.Database.CanConnectAsync(cancellationToken);
            return new HealthCheckStatus(reachable ? "ok" : "fail", reachable, SanitizeMode(mode));
        }
        catch
        {
            return new HealthCheckStatus("fail", false, SanitizeMode(mode));
        }
    }

    private async Task<HealthCheckStatus> CheckStorageAsync(CancellationToken cancellationToken)
    {
        var mode = configuration["Storage:Mode"] ?? "Local";
        try
        {
            var reachable = await storage.CanReachAsync(cancellationToken);
            return new HealthCheckStatus(reachable ? "ok" : "fail", reachable, SanitizeMode(mode));
        }
        catch
        {
            return new HealthCheckStatus("fail", false, SanitizeMode(mode));
        }
    }

    private async Task<HealthCheckStatus> CheckQueueAsync(CancellationToken cancellationToken)
    {
        var mode = configuration["Queue:Mode"] ?? configuration["Storage:Mode"] ?? "InMemory";
        try
        {
            var reachable = await queue.CanReachAsync(cancellationToken);
            return new HealthCheckStatus(reachable ? "ok" : "fail", reachable, SanitizeMode(mode));
        }
        catch
        {
            return new HealthCheckStatus("fail", false, SanitizeMode(mode));
        }
    }

    private static string SanitizeMode(string mode)
    {
        if (string.Equals(mode, "Azure", StringComparison.OrdinalIgnoreCase)) return "Azure";
        if (string.Equals(mode, "SqlServer", StringComparison.OrdinalIgnoreCase)) return "SqlServer";
        if (string.Equals(mode, "Sqlite", StringComparison.OrdinalIgnoreCase)) return "Sqlite";
        if (string.Equals(mode, "InMemory", StringComparison.OrdinalIgnoreCase)) return "InMemory";
        if (string.Equals(mode, "Local", StringComparison.OrdinalIgnoreCase)) return "Local";
        if (string.Equals(mode, "Http", StringComparison.OrdinalIgnoreCase)) return "Http";
        if (string.Equals(mode, "Mock", StringComparison.OrdinalIgnoreCase)) return "Mock";
        return "unknown";
    }
}
