using System.Text.Json;
using Azure;
using Azure.AI.DocumentIntelligence;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Ocr;

namespace DeedAi.Infrastructure.Ocr;

public sealed class AzureDocumentIntelligenceClient(DocumentIntelligenceClient client) : IDocumentIntelligenceClient
{
    public async Task<DocumentIntelligenceResult> AnalyzeAsync(
        string documentName,
        Stream pdf,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await pdf.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var operation = await client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-layout",
            BinaryData.FromStream(buffer),
            cancellationToken: cancellationToken);
        var result = operation.Value;

        var notes = FirstContent(result, "Notes");
        if (notes is { Length: > 500 })
        {
            notes = notes[..500];
        }

        var fields = new ExtractedDeedFields
        {
            Grantor = FirstContent(result, "Grantor", "Seller"),
            Grantee = FirstContent(result, "Grantee", "Buyer"),
            InstrumentDate = FirstContent(result, "InstrumentDate", "Date"),
            Consideration = FirstContent(result, "Consideration", "Amount"),
            ParcelId = FirstContent(result, "ParcelId", "Parcel"),
            Client = null,
            Notes = notes
        };

        return new DocumentIntelligenceResult
        {
            RawJson = JsonSerializer.Serialize(new
            {
                source = "azure-document-intelligence",
                documentName,
                modelId = result.ModelId,
                content = result.Content,
                analyzedAt = DateTimeOffset.UtcNow
            }),
            Fields = fields
        };
    }

    public async Task<bool> CanReachAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Invalid payload on purpose: 4xx means the endpoint answered. Never log endpoint or key.
            await client.AnalyzeDocumentAsync(
                WaitUntil.Started,
                "prebuilt-layout",
                BinaryData.FromBytes("x"u8.ToArray()),
                cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is >= 400 and < 500)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FirstContent(AnalyzeResult result, params string[] keys)
    {
        if (result.Documents is not { Count: > 0 })
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (result.Documents[0].Fields.TryGetValue(key, out var field)
                && !string.IsNullOrWhiteSpace(field.Content))
            {
                return field.Content;
            }
        }

        return null;
    }
}
