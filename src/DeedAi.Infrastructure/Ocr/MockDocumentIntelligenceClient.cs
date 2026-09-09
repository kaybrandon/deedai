using System.Text.Json;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Ocr;

namespace DeedAi.Infrastructure.Ocr;

public sealed class MockDocumentIntelligenceClient : IDocumentIntelligenceClient
{
    public Task<DocumentIntelligenceResult> AnalyzeAsync(
        string documentName,
        Stream pdf,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = pdf;

        if (documentName.Contains("bad", StringComparison.OrdinalIgnoreCase)
            || documentName.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("OCR failed — mock Document Intelligence rejected the file.");
        }

        var fields = new ExtractedDeedFields
        {
            Grantor = "Jane Example",
            Grantee = "Acme Holdings LLC",
            InstrumentDate = "2024-08-12",
            Consideration = "250000",
            ParcelId = "12-345-678",
            Client = "Acme",
            Notes = "Mock Document Intelligence (no DI keys configured)."
        };

        var raw = JsonSerializer.Serialize(new
        {
            source = "mock",
            documentName,
            analyzedAt = DateTimeOffset.UtcNow,
            fields
        });

        return Task.FromResult(new DocumentIntelligenceResult
        {
            RawJson = raw,
            Fields = fields
        });
    }

    public Task<bool> CanReachAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}
