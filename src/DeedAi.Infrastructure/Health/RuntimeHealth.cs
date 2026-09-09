using System.Globalization;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DeedAi.Infrastructure.Health;

public sealed record HealthCheckStatus(string Status, bool Reachable, string? Mode = null, string? Detail = null);

public sealed record ShallowHealth(string Status, string Product);

public sealed record DetailedHealth(
    string Status,
    string Product,
    HealthCheckStatus Sql,
    HealthCheckStatus Blob,
    HealthCheckStatus OcrQueue,
    HealthCheckStatus DocumentIntelligence,
    HealthCheckStatus OcrPipeline)
{
    public HealthCheckStatus Storage => Blob;
    public HealthCheckStatus Queue => OcrQueue;
}

public sealed class RuntimeHealth(
    DeedAiDbContext db,
    IBlobStorage storage,
    IOcrJobQueue queue,
    IDocumentIntelligenceClient documentIntelligence,
    OcrPipelineSignal ocrSignal,
    IConfiguration configuration)
{
    public const string ProductName = "Deed AI";
    private const string CanaryPrefix = "health/canary-";

    public static ShallowHealth Shallow() => new("ok", ProductName);

    public async Task<DetailedHealth> DetailAsync(CancellationToken cancellationToken)
    {
        var sql = await CheckSqlAsync(cancellationToken);
        var blob = await CheckBlobAsync(cancellationToken);
        var ocrQueue = await CheckOcrQueueAsync(cancellationToken);
        var di = await CheckDocumentIntelligenceAsync(cancellationToken);
        var pipeline = await CheckOcrPipelineAsync(ocrQueue, cancellationToken);
        var overall = sql.Reachable && blob.Reachable && ocrQueue.Reachable && di.Reachable && pipeline.Reachable
            ? "ok"
            : "degraded";
        return new DetailedHealth(overall, ProductName, sql, blob, ocrQueue, di, pipeline);
    }

    private async Task<HealthCheckStatus> CheckSqlAsync(CancellationToken cancellationToken)
    {
        var mode = SanitizeMode(configuration["Database:Provider"] ?? "Sqlite");
        try
        {
            var reachable = await db.Database.CanConnectAsync(cancellationToken);
            return new HealthCheckStatus(reachable ? "ok" : "fail", reachable, mode);
        }
        catch
        {
            return new HealthCheckStatus("fail", false, mode);
        }
    }

    private async Task<HealthCheckStatus> CheckBlobAsync(CancellationToken cancellationToken)
    {
        var mode = SanitizeMode(configuration["Storage:Mode"] ?? "Local");
        var path = $"{CanaryPrefix}{Guid.NewGuid():N}.txt";
        var uploaded = false;
        try
        {
            if (!await storage.CanReachAsync(cancellationToken))
            {
                return new HealthCheckStatus("fail", false, mode, "Unreachable");
            }

            var payload = "deedai-health"u8.ToArray();
            await using (var write = new MemoryStream(payload))
            {
                await storage.UploadAsync(path, write, "text/plain", cancellationToken);
                uploaded = true;
            }

            await using var read = await storage.OpenReadAsync(path, cancellationToken);
            using var copy = new MemoryStream();
            await read.CopyToAsync(copy, cancellationToken);
            var matched = copy.ToArray().AsSpan().SequenceEqual(payload);
            return matched
                ? new HealthCheckStatus("ok", true, mode, "Read/write")
                : new HealthCheckStatus("fail", false, mode, "Read mismatch");
        }
        catch
        {
            return new HealthCheckStatus("fail", false, mode, "Read/write failed");
        }
        finally
        {
            if (uploaded)
            {
                try
                {
                    await storage.DeleteAsync(path, CancellationToken.None);
                }
                catch
                {
                    // Canary cleanup must never surface paths or storage errors.
                }
            }
        }
    }

    private async Task<HealthCheckStatus> CheckOcrQueueAsync(CancellationToken cancellationToken)
    {
        var mode = SanitizeMode(configuration["Queue:Mode"] ?? configuration["Storage:Mode"] ?? "InMemory");
        try
        {
            var reachable = await queue.CanReachAsync(cancellationToken);
            return new HealthCheckStatus(reachable ? "ok" : "fail", reachable, mode);
        }
        catch
        {
            return new HealthCheckStatus("fail", false, mode);
        }
    }

    private async Task<HealthCheckStatus> CheckDocumentIntelligenceAsync(CancellationToken cancellationToken)
    {
        var configured = !string.IsNullOrWhiteSpace(
                             DeedAi.Infrastructure.DependencyInjection.FirstValue(
                                 configuration, "DocumentIntelligenceKey", "DocumentIntelligence:Key"))
                         && !string.IsNullOrWhiteSpace(
                             DeedAi.Infrastructure.DependencyInjection.FirstValue(
                                 configuration, "BISDocumentIntelligenceEndpoint", "DocumentIntelligence:Endpoint"));
        var mode = SanitizeMode(configured ? "Azure" : "Mock");
        try
        {
            var reachable = await documentIntelligence.CanReachAsync(cancellationToken);
            return new HealthCheckStatus(reachable ? "ok" : "fail", reachable, mode);
        }
        catch
        {
            return new HealthCheckStatus("fail", false, mode);
        }
    }

    private async Task<HealthCheckStatus> CheckOcrPipelineAsync(
        HealthCheckStatus queueCheck,
        CancellationToken cancellationToken)
    {
        var mode = queueCheck.Mode ?? "unknown";
        if (!queueCheck.Reachable)
        {
            return new HealthCheckStatus("fail", false, mode, "Queue unreachable");
        }

        try
        {
            var lastReady = await db.Documents.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(x => x.Status == DocumentStatuses.Ready)
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x => (DateTimeOffset?)x.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var lastSuccess = ocrSignal.LastSuccessAt ?? lastReady;
            var detail = lastSuccess is null
                ? "Queue reachable · no Ready yet"
                : $"Queue reachable · last Ready {lastSuccess.Value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            return new HealthCheckStatus("ok", true, mode, detail);
        }
        catch
        {
            return new HealthCheckStatus("ok", true, mode, "Queue reachable");
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
