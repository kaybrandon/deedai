using DeedAi.Domain.Ocr;

namespace DeedAi.Domain.Abstractions;

public interface IDocumentIntelligenceClient
{
    Task<DocumentIntelligenceResult> AnalyzeAsync(
        string documentName,
        Stream pdf,
        CancellationToken cancellationToken);
}
