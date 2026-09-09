namespace DeedAi.Infrastructure.Health;

/// <summary>In-process OCR / DI timestamps. Never stores secrets, endpoints, or document contents.</summary>
public sealed class OcrHealthSignal
{
    private readonly object _gate = new();

    public DateTimeOffset? LastHeartbeatAt { get; private set; }
    public DateTimeOffset? LastDequeueAt { get; private set; }
    public DateTimeOffset? LastDiSuccessAt { get; private set; }
    public DateTimeOffset? LastDiFailAt { get; private set; }
    public DateTimeOffset LastHeartbeatPersistAt { get; private set; } = DateTimeOffset.MinValue;

    public void RecordHeartbeat() => Set(at => LastHeartbeatAt = at);

    public bool ShouldPersistHeartbeat(TimeSpan interval)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - LastHeartbeatPersistAt < interval)
            {
                return false;
            }

            LastHeartbeatPersistAt = now;
            return true;
        }
    }

    public void MarkHeartbeatPersisted()
    {
        lock (_gate)
        {
            LastHeartbeatPersistAt = DateTimeOffset.UtcNow;
        }
    }

    public void RecordDequeue()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            LastHeartbeatAt = now;
            LastDequeueAt = now;
        }
    }

    public void RecordDiSuccess()
    {
        lock (_gate)
        {
            LastDiSuccessAt = DateTimeOffset.UtcNow;
        }
    }

    public void RecordDiFail()
    {
        lock (_gate)
        {
            LastDiFailAt = DateTimeOffset.UtcNow;
        }
    }

    public void Hydrate(
        DateTimeOffset? heartbeat,
        DateTimeOffset? dequeue,
        DateTimeOffset? diSuccess,
        DateTimeOffset? diFail)
    {
        lock (_gate)
        {
            LastHeartbeatAt = Later(LastHeartbeatAt, heartbeat);
            LastDequeueAt = Later(LastDequeueAt, dequeue);
            LastDiSuccessAt = Later(LastDiSuccessAt, diSuccess);
            LastDiFailAt = Later(LastDiFailAt, diFail);
        }
    }

    private void Set(Action<DateTimeOffset> assign)
    {
        lock (_gate)
        {
            assign(DateTimeOffset.UtcNow);
        }
    }

    private static DateTimeOffset? Later(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (left is null) return right;
        if (right is null) return left;
        return right > left ? right : left;
    }
}

public sealed record OcrHealthSnapshot(
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset? LastDequeueAt,
    DateTimeOffset? LastDiSuccessAt,
    DateTimeOffset? LastDiFailAt);
