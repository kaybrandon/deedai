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

public sealed class ExtractedReviewFields
{
    public string? DocumentNumber { get; init; }
    public string? Volume { get; init; }
    public string? Page { get; init; }
    public string? DeedType { get; init; }
    public string? Pid { get; init; }
    public string? MailingStreet { get; init; }
    public string? MailingCity { get; init; }
    public string? MailingState { get; init; }
    public string? MailingZip { get; init; }
    public IReadOnlyList<string>? Grantors { get; init; }
    public IReadOnlyList<string>? Grantees { get; init; }
    public string? InstrumentDate { get; init; }
    public string? Consideration { get; init; }
    public string? Client { get; init; }
    public string? Notes { get; init; }

    public string? PrimaryGrantor => Grantors?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    public string? PrimaryGrantee => Grantees?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
}

public sealed class AiExtractResult
{
    public required string RawJson { get; init; }
    public required ExtractedReviewFields Fields { get; init; }
    public IReadOnlyDictionary<string, double> Confidence { get; init; } = new Dictionary<string, double>();
    public string Source { get; init; } = "AzureOpenAI";
}
