using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeedAi.Infrastructure.Data;

public static class MigrationRunner
{
    /// <summary>
    /// Applies pending EF migrations. A second attempt covers a partial apply
    /// (DDL landed, later step failed, history not written) when the migration
    /// is idempotent. Exception text is not logged — it can contain connection details.
    /// </summary>
    public static async Task MigrateAsync(
        DeedAiDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("No pending EF migrations.");
        }
        else
        {
            logger.LogInformation(
                "Applying {Count} pending EF migrations: {Migrations}",
                pending.Count,
                string.Join(", ", pending));
        }

        try
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception first)
        {
            logger.LogWarning(
                "MigrateAsync failed ({ExceptionType}); retrying once after a possible partial apply. Details omitted to avoid leaking connection data.",
                first.GetType().Name);
            await db.Database.MigrateAsync(cancellationToken);
        }
    }
}
