using System.Globalization;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Infrastructure.Health;

public sealed class OcrHealthRecorder(DeedAiDbContext db, OcrHealthSignal signal)
{
    private static readonly TimeSpan HeartbeatPersistInterval = TimeSpan.FromSeconds(15);

    public async Task RecordWorkerHeartbeatAsync(CancellationToken cancellationToken)
    {
        signal.RecordHeartbeat();
        if (!signal.ShouldPersistHeartbeat(HeartbeatPersistInterval))
        {
            return;
        }

        await PersistAsync(AppSetting.OcrWorkerHeartbeatKey, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task RecordDequeueAsync(CancellationToken cancellationToken)
    {
        signal.RecordDequeue();
        signal.MarkHeartbeatPersisted();
        var now = DateTimeOffset.UtcNow;
        await PersistAsync(AppSetting.OcrWorkerHeartbeatKey, now, cancellationToken);
        await PersistAsync(AppSetting.OcrLastDequeueKey, now, cancellationToken);
    }

    public async Task RecordDiOutcomeAsync(bool success, CancellationToken cancellationToken)
    {
        if (success)
        {
            signal.RecordDiSuccess();
            await PersistAsync(AppSetting.DiLastSuccessKey, DateTimeOffset.UtcNow, cancellationToken);
            return;
        }

        signal.RecordDiFail();
        await PersistAsync(AppSetting.DiLastFailKey, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task<OcrHealthSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var rows = await db.AppSettings.AsNoTracking()
            .Where(x =>
                x.Key == AppSetting.OcrWorkerHeartbeatKey
                || x.Key == AppSetting.OcrLastDequeueKey
                || x.Key == AppSetting.DiLastSuccessKey
                || x.Key == AppSetting.DiLastFailKey)
            .ToListAsync(cancellationToken);

        var heartbeat = Parse(rows, AppSetting.OcrWorkerHeartbeatKey);
        var dequeue = Parse(rows, AppSetting.OcrLastDequeueKey);
        var success = Parse(rows, AppSetting.DiLastSuccessKey);
        var fail = Parse(rows, AppSetting.DiLastFailKey);
        signal.Hydrate(heartbeat, dequeue, success, fail);
        return new OcrHealthSnapshot(
            Later(signal.LastHeartbeatAt, heartbeat),
            Later(signal.LastDequeueAt, dequeue),
            Later(signal.LastDiSuccessAt, success),
            Later(signal.LastDiFailAt, fail));
    }

    private async Task PersistAsync(string key, DateTimeOffset at, CancellationToken cancellationToken)
    {
        try
        {
            var value = at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            var row = await db.AppSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (row is null)
            {
                db.AppSettings.Add(new AppSetting
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = at
                });
            }
            else
            {
                row.Value = value;
                row.UpdatedAt = at;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Health timestamps must never fail OCR or the worker loop.
        }
    }

    private static DateTimeOffset? Parse(IReadOnlyCollection<AppSetting> rows, string key)
    {
        var raw = rows.FirstOrDefault(x => x.Key == key)?.Value;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static DateTimeOffset? Later(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (left is null) return right;
        if (right is null) return left;
        return right > left ? right : left;
    }
}
