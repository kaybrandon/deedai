using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Ocr;

namespace DeedAi.Infrastructure.Ocr;

public sealed class FailClosedAiExtractClient : IAiExtractClient
{
    public const string Message =
        "Azure OpenAI is not configured. Extract is closed. Set AzureOpenAIEndpoint, AzureOpenAIKey, and AzureOpenAIDeployment in Key Vault.";

    public Task<AiExtractResult> ExtractAsync(string documentName, Stream pdf, CancellationToken cancellationToken)
    {
        _ = documentName;
        _ = pdf;
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(Message);
    }

    public Task<bool> CanReachAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
