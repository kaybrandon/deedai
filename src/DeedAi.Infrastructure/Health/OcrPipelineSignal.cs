namespace DeedAi.Infrastructure.Health;

/// <summary>In-process last OCR attempt/success. Never stores secrets or document contents.</summary>
public sealed class OcrPipelineSignal
{
    private readonly object _gate = new();

    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? LastSuccessAt { get; private set; }

    public void RecordAttempt()
    {
        lock (_gate)
        {
            LastAttemptAt = DateTimeOffset.UtcNow;
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            LastAttemptAt = now;
            LastSuccessAt = now;
        }
    }
}
