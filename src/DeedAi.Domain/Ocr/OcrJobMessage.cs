namespace DeedAi.Domain.Ocr;

public sealed class OcrJobMessage
{
    public Guid DocumentId { get; set; }
    public string BlobPath { get; set; } = "";
}

public sealed class OcrQueueDelivery
{
    public required OcrJobMessage Job { get; init; }
    public required string MessageId { get; init; }
    public required string PopReceipt { get; init; }
    public int DequeueCount { get; init; }
}

public sealed record OcrQueueSnapshot(int Depth, DateTimeOffset? OldestWaitingAt, int PoisonCount);

public sealed class DocumentIntelligenceResult
{
    public required string RawJson { get; init; }
    public required ExtractedDeedFields Fields { get; init; }
}

public sealed class ExtractedDeedFields
{
    public string? Grantor { get; init; }
    public string? Grantee { get; init; }
    public string? InstrumentDate { get; init; }
    public string? Consideration { get; init; }
    public string? ParcelId { get; init; }
    public string? Client { get; init; }
    public string? Notes { get; init; }
}
