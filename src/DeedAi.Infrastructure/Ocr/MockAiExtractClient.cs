using System.Text.Json;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Ocr;

namespace DeedAi.Infrastructure.Ocr;

public sealed class MockAiExtractClient : IAiExtractClient
{
    public Task<AiExtractResult> ExtractAsync(string documentName, Stream pdf, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = pdf;

        if (documentName.Contains("bad", StringComparison.OrdinalIgnoreCase)
            || documentName.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("AI extract failed — mock Azure OpenAI rejected the file.");
        }

        if (documentName.Contains("broken.json", StringComparison.OrdinalIgnoreCase)
            || documentName.Contains("invalid-json", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new AiExtractResult
            {
                RawJson = "not-json{",
                Fields = new ExtractedReviewFields(),
                Source = "Mock"
            });
        }

        var fields = new ExtractedReviewFields
        {
            DocumentNumber = "2024-0812",
            Volume = "184",
            Page = "12",
            DeedType = "Warranty Deed",
            Pid = documentName.Contains("dirty", StringComparison.OrdinalIgnoreCase)
                  || documentName.Contains("clean_me", StringComparison.OrdinalIgnoreCase)
                ? "N/A"
                : "12-345-678",
            MailingStreet = "100 Main St",
            MailingCity = "Springfield",
            MailingState = "IL",
            MailingZip = "62701",
            Grantors = documentName.Contains("dirty", StringComparison.OrdinalIgnoreCase)
                       || documentName.Contains("clean_me", StringComparison.OrdinalIgnoreCase)
                ? ["\"Jane Example\""]
                : ["Jane Example"],
            Grantees = ["Acme Holdings LLC"],
            InstrumentDate = "2024-08-12",
            Consideration = "250000",
            Client = "Acme",
            Notes = "Mock Azure OpenAI extract (Mode=Mock)."
        };

        var confidence = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["documentNumber"] = 0.94,
            ["volume"] = 0.88,
            ["page"] = 0.81,
            ["deedType"] = 0.91,
            ["pid"] = 0.72,
            ["mailingStreet"] = 0.86,
            ["mailingCity"] = 0.9,
            ["mailingState"] = 0.95,
            ["mailingZip"] = 0.93,
            ["grantors"] = 0.9,
            ["grantees"] = 0.87
        };

        var raw = JsonSerializer.Serialize(new
        {
            source = "mock",
            documentName,
            analyzedAt = DateTimeOffset.UtcNow,
            fields,
            confidence
        });

        return Task.FromResult(new AiExtractResult
        {
            RawJson = raw,
            Fields = fields,
            Confidence = confidence,
            Source = "Mock"
        });
    }

    public Task<bool> CanReachAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}
