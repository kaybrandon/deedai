using DeedAi.Domain.Ocr;

namespace DeedAi.Domain.Abstractions;

public interface IOcrJobQueue
{
    Task EnqueueAsync(OcrJobMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Long-polls the OCR queue. Azure Storage Queue waits up to ~30s server-side
    /// instead of a 1-second busy poll.
    /// </summary>
    Task<OcrQueueDelivery?> ReceiveAsync(TimeSpan visibilityTimeout, CancellationToken cancellationToken);

    Task DeleteAsync(OcrQueueDelivery delivery, CancellationToken cancellationToken);

    /// <summary>Reachability only — never return connection strings or keys.</summary>
    Task<bool> CanReachAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Azure queue is the bulk buffer — peek/properties only. Never return message bodies,
    /// connection strings, or keys.
    /// </summary>
    Task<OcrQueueSnapshot> GetSnapshotAsync(int poisonDequeueCount, CancellationToken cancellationToken);
}
