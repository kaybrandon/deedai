using DeedAi.Domain.Ocr;

namespace DeedAi.Domain.Abstractions;

public interface IAiExtractClient
{
    Task<AiExtractResult> ExtractAsync(string documentName, Stream pdf, CancellationToken cancellationToken);

    /// <summary>Endpoint reachability only — never return endpoints, keys, or payloads.</summary>
    Task<bool> CanReachAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}
