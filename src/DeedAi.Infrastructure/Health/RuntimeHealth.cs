using System.Globalization;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Ocr;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Ocr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DeedAi.Infrastructure.Health;

public sealed record HealthCheckStatus(
    string Status,
    bool Reachable,
    string? Mode = null,
    string? Detail = null,
    bool? Configured = null);

public sealed record ShallowHealth(string Status, string Product);

public sealed record OcrQueueVisibility(
    int Depth,
    int? OldestWaitingAgeSeconds,
    int PoisonCount,
    int FailedCount,
    string? LastDiSuccessAt,
    string? LastDiFailAt);

public sealed record DetailedHealth(
    string Status,
    string Product,
    HealthCheckStatus Sql,
    HealthCheckStatus Storage,
    HealthCheckStatus Queue,
    HealthCheckStatus Blob,
    HealthCheckStatus DocumentIntelligence,
    HealthCheckStatus AzureOpenAI,
    HealthCheckStatus OcrPipeline,
    OcrQueueVisibility OcrQueue);

public sealed class RuntimeHealth(
    DeedAiDbContext db,
    IBlobStorage storage,
    IOcrJobQueue queue,
    IAiExtractClient extract,
    OcrHealthRecorder ocrHealth,
    IOptions<OcrOptions> ocrOptions,
    IConfiguration configuration)
{
    public const string ProductName = "Deed AI";
    public static readonly TimeSpan WorkerFreshness = TimeSpan.FromMinutes(3);
    private const string CanaryPrefix = "health/canary-";

    public static ShallowHealth Shallow() => new("ok", ProductName);

    public async Task<DetailedHealth> DetailAsync(CancellationToken cancellationToken)
    {
        var sql = await CheckSqlAsync(cancellationToken);
        var storageCheck = await CheckStorageAsync(cancellationToken);
        var queueCheck = await CheckQueueAsync(cancellationToken);
        var blob = await CheckBlobReadWriteAsync(cancellationToken);
        var di = DocumentIntelligenceRemoved();
        var azureOpenAi = await CheckAzureOpenAIAsync(cancellationToken);
        var signals = await ocrHealth.ReadAsync(cancellationToken);
        var pipeline = CheckOcrPipeline(queueCheck, signals);
        var visibility = await ReadQueueVisibilityAsync(signals, cancellationToken);
        var overall = sql.Reachable
                      && storageCheck.Reachable
                      && queueCheck.Reachable
                      && blob.Reachable
                      && azureOpenAi.Reachable
                      && pipeline.Reachable
            ? "ok"
            : "degraded";
        return new DetailedHealth(
            overall,
            ProductName,
            sql,
            storageCheck,
            queueCheck,
            blob,
            di,
            azureOpenAi,
            pipeline,
            visibility);
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

    private async Task<HealthCheckStatus> CheckStorageAsync(CancellationToken cancellationToken)
    {
        var mode = SanitizeMode(configuration["Storage:Mode"] ?? "Local");
        try
        {
            var reachable = await storage.CanReachAsync(cancellationToken);
            return new HealthCheckStatus(reachable ? "ok" : "fail", reachable, mode);
        }
        catch
        {
            return new HealthCheckStatus("fail", false, mode);
        }
    }

    private async Task<HealthCheckStatus> CheckQueueAsync(CancellationToken cancellationToken)
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

    private async Task<HealthCheckStatus> CheckBlobReadWriteAsync(CancellationToken cancellationToken)
    {
        var mode = SanitizeMode(configuration["Storage:Mode"] ?? "Local");
        var path = $"{CanaryPrefix}{Guid.NewGuid():N}.txt";
        var uploaded = false;
        try
        {
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
                ? new HealthCheckStatus("ok", true, mode, "Pass")
                : new HealthCheckStatus("fail", false, mode, "Fail");
        }
        catch
        {
            return new HealthCheckStatus("fail", false, mode, "Fail");
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

    private static HealthCheckStatus DocumentIntelligenceRemoved() =>
        new("ok", true, "Removed", "Removed — AI extract only", false);

    private async Task<HealthCheckStatus> CheckAzureOpenAIAsync(CancellationToken cancellationToken)
    {
        var options = DependencyInjection.BindAzureOpenAI(configuration);
        var mock = string.Equals(options.Mode, "Mock", StringComparison.OrdinalIgnoreCase);
        var configured = mock
                         || (HasRealValue(options.Endpoint) && HasRealValue(options.Key) && HasRealValue(options.Deployment));
        var mode = SanitizeMode(mock ? "Mock" : configured ? "Azure" : "Unconfigured");
        try
        {
            var reachable = await extract.CanReachAsync(cancellationToken);
            var detail = mock
                ? "Mock extract"
                : configured
                    ? "Field fill"
                    : "Fail closed";
            return new HealthCheckStatus(reachable ? "ok" : "fail", reachable, mode, detail, configured);
        }
        catch
        {
            return new HealthCheckStatus("fail", false, mode, "Fail closed", configured);
        }
    }

    private static HealthCheckStatus CheckOcrPipeline(HealthCheckStatus queueCheck, OcrHealthSnapshot signals)
    {
        var mode = queueCheck.Mode ?? "unknown";
        if (!queueCheck.Reachable)
        {
            return new HealthCheckStatus("fail", false, mode, "Queue unreachable");
        }

        var now = DateTimeOffset.UtcNow;
        if (IsFresh(signals.LastHeartbeatAt, now))
        {
            return new HealthCheckStatus("ok", true, mode, "Queue reachable · worker heartbeat fresh");
        }

        if (IsFresh(signals.LastDequeueAt, now))
        {
            return new HealthCheckStatus("ok", true, mode, "Queue reachable · recent dequeue");
        }

        if (signals.LastHeartbeatAt is null && signals.LastDequeueAt is null)
        {
            return new HealthCheckStatus("fail", false, mode, "Queue reachable · no worker heartbeat");
        }

        return new HealthCheckStatus("fail", false, mode, "Queue reachable · worker heartbeat stale");
    }

    private async Task<OcrQueueVisibility> ReadQueueVisibilityAsync(
        OcrHealthSnapshot signals,
        CancellationToken cancellationToken)
    {
        var poisonThreshold = Math.Max(1, ocrOptions.Value.PoisonDequeueCount);
        OcrQueueSnapshot snapshot;
        try
        {
            snapshot = await queue.GetSnapshotAsync(poisonThreshold, cancellationToken);
        }
        catch
        {
            snapshot = new OcrQueueSnapshot(0, null, 0);
        }

        var failedCount = 0;
        try
        {
            failedCount = await db.Documents.AsNoTracking()
                .CountAsync(x => x.Status == DocumentStatuses.Failed, cancellationToken);
        }
        catch
        {
            failedCount = 0;
        }

        int? ageSeconds = null;
        if (snapshot.OldestWaitingAt is { } oldest)
        {
            ageSeconds = Math.Max(0, (int)Math.Round((DateTimeOffset.UtcNow - oldest).TotalSeconds));
        }

        return new OcrQueueVisibility(
            snapshot.Depth,
            ageSeconds,
            snapshot.PoisonCount,
            failedCount,
            FormatTimestamp(signals.LastDiSuccessAt),
            FormatTimestamp(signals.LastDiFailAt));
    }

    private static bool HasRealValue(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !value.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);

    private static bool IsFresh(DateTimeOffset? at, DateTimeOffset now) =>
        at is { } value && now - value <= WorkerFreshness;

    private static string? FormatTimestamp(DateTimeOffset? at) =>
        at?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    internal static string SanitizeMode(string mode)
    {
        if (string.Equals(mode, "Azure", StringComparison.OrdinalIgnoreCase)) return "Azure";
        if (string.Equals(mode, "SqlServer", StringComparison.OrdinalIgnoreCase)) return "SqlServer";
        if (string.Equals(mode, "Sqlite", StringComparison.OrdinalIgnoreCase)) return "Sqlite";
        if (string.Equals(mode, "InMemory", StringComparison.OrdinalIgnoreCase)) return "InMemory";
        if (string.Equals(mode, "Local", StringComparison.OrdinalIgnoreCase)) return "Local";
        if (string.Equals(mode, "Http", StringComparison.OrdinalIgnoreCase)) return "Http";
        if (string.Equals(mode, "Mock", StringComparison.OrdinalIgnoreCase)) return "Mock";
        if (string.Equals(mode, "Unconfigured", StringComparison.OrdinalIgnoreCase)) return "Unconfigured";
        if (string.Equals(mode, "Removed", StringComparison.OrdinalIgnoreCase)) return "Removed";
        return "unknown";
    }
}
