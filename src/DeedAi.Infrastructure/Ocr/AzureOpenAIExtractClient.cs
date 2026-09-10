using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Ocr;
using Microsoft.Extensions.Options;

namespace DeedAi.Infrastructure.Ocr;

public sealed class AzureOpenAIExtractClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AzureOpenAIOptions> options) : IAiExtractClient
{
    public const string HttpClientName = nameof(AzureOpenAIExtractClient);
    private const int MaxPdfBytes = 4_000_000;

    public async Task<AiExtractResult> ExtractAsync(string documentName, Stream pdf, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        EnsureCheapModel(settings);

        await using var copy = new MemoryStream();
        await pdf.CopyToAsync(copy, cancellationToken);
        var bytes = copy.ToArray();
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("AI extract failed — empty PDF. Use Retry.");
        }

        var payload = BuildRequest(documentName, bytes);
        using var request = new HttpRequestMessage(HttpMethod.Post, CompletionsUri(settings))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("api-key", settings.Key);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("AI extract failed — Azure OpenAI rejected the request. Use Retry.");
        }

        var content = ReadAssistantContent(body);
        return ParseExtract(content);
    }

    public async Task<bool> CanReachAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.Key))
        {
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ModelsUri(settings));
            request.Headers.TryAddWithoutValidation("api-key", settings.Key);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            return (int)response.StatusCode is >= 200 and < 500;
        }
        catch
        {
            return false;
        }
    }

    internal static void EnsureCheapModel(AzureOpenAIOptions settings)
    {
        if (settings.AllowPricierModel)
        {
            return;
        }

        var model = AiExtractModels.Normalize(settings.Model);
        if (AiExtractModels.IsPricier(model))
        {
            throw new InvalidOperationException(AiExtractModels.EscalateMessage(model));
        }

        // Azure calls the deployment. A pricier deployment name must not slip past a cheap Model default.
        if (!string.IsNullOrWhiteSpace(settings.Deployment)
            && AiExtractModels.IsPricier(settings.Deployment))
        {
            throw new InvalidOperationException(AiExtractModels.EscalateMessage(settings.Deployment));
        }
    }

    internal static AiExtractResult ParseExtract(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("AI extract failed — empty JSON. Use Retry.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("AI extract failed — invalid JSON. Use Retry.");
        }

        using (document)
        {
            var root = document.RootElement;
            var fieldsNode = root.TryGetProperty("fields", out var nested) ? nested : root;
            var grantors = ReadStringList(fieldsNode, "grantors");
            var grantees = ReadStringList(fieldsNode, "grantees");
            var fields = new ExtractedReviewFields
            {
                DocumentNumber = ReadString(fieldsNode, "documentNumber"),
                Volume = ReadString(fieldsNode, "volume"),
                Page = ReadString(fieldsNode, "page"),
                DeedType = ReadString(fieldsNode, "deedType"),
                Pid = ReadString(fieldsNode, "pid") ?? ReadString(fieldsNode, "parcelId"),
                MailingStreet = ReadString(fieldsNode, "mailingStreet"),
                MailingCity = ReadString(fieldsNode, "mailingCity"),
                MailingState = ReadString(fieldsNode, "mailingState"),
                MailingZip = ReadString(fieldsNode, "mailingZip"),
                Grantors = grantors.Count > 0 ? grantors : OptionalList(ReadString(fieldsNode, "grantor")),
                Grantees = grantees.Count > 0 ? grantees : OptionalList(ReadString(fieldsNode, "grantee")),
                InstrumentDate = ReadString(fieldsNode, "instrumentDate"),
                Consideration = ReadString(fieldsNode, "consideration"),
                Client = ReadString(fieldsNode, "client"),
                Notes = ReadString(fieldsNode, "notes")
            };

            var confidence = new Dictionary<string, double>(StringComparer.Ordinal);
            if (root.TryGetProperty("confidence", out var scores) && scores.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in scores.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Number
                        && property.Value.TryGetDouble(out var score))
                    {
                        confidence[property.Name] = score;
                    }
                }
            }

            return new AiExtractResult
            {
                RawJson = content,
                Fields = fields,
                Confidence = confidence,
                Source = "AzureOpenAI"
            };
        }
    }

    private static string BuildRequest(string documentName, byte[] pdf)
    {
        var text = PdfTextHint.FromBytes(pdf);
        var userText = new StringBuilder()
            .AppendLine("Extract deed Review fields as JSON. Use only these names:")
            .AppendLine("documentNumber volume page deedType pid mailingStreet mailingCity mailingState mailingZip grantors grantees instrumentDate consideration client notes")
            .AppendLine("Also include confidence as 0-1 numbers for those keys. Never invent County or CAMA names. Empty string or [] when unknown.")
            .AppendLine($"Document name: {documentName}")
            .AppendLine("PDF text hint:")
            .Append(text)
            .ToString();

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("temperature", 0);
            writer.WritePropertyName("response_format");
            writer.WriteStartObject();
            writer.WriteString("type", "json_object");
            writer.WriteEndObject();
            writer.WritePropertyName("messages");
            writer.WriteStartArray();
            WriteTextMessage(writer, "system", "You extract locked Review fields from a Texas/US deed PDF. Return JSON only.");
            writer.WriteStartObject();
            writer.WriteString("role", "user");
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", userText);
            writer.WriteEndObject();
            if (pdf.Length <= MaxPdfBytes)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "file");
                writer.WritePropertyName("file");
                writer.WriteStartObject();
                writer.WriteString("filename", string.IsNullOrWhiteSpace(documentName) ? "deed.pdf" : documentName);
                writer.WriteString("file_data", "data:application/pdf;base64," + Convert.ToBase64String(pdf));
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteTextMessage(Utf8JsonWriter writer, string role, string text)
    {
        writer.WriteStartObject();
        writer.WriteString("role", role);
        writer.WriteString("content", text);
        writer.WriteEndObject();
    }

    private static Uri CompletionsUri(AzureOpenAIOptions settings)
    {
        var endpoint = (settings.Endpoint ?? "").TrimEnd('/');
        var deployment = Uri.EscapeDataString(settings.Deployment ?? "");
        var version = Uri.EscapeDataString(settings.ApiVersion);
        return new Uri($"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version={version}");
    }

    private static Uri ModelsUri(AzureOpenAIOptions settings)
    {
        var endpoint = (settings.Endpoint ?? "").TrimEnd('/');
        var version = Uri.EscapeDataString(settings.ApiVersion);
        return new Uri($"{endpoint}/openai/models?api-version={version}");
    }

    private static string ReadAssistantContent(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("choices", out var choices)
                && choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                if (message.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString() ?? "";
                }
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("AI extract failed — invalid JSON. Use Retry.");
        }

        throw new InvalidOperationException("AI extract failed — empty JSON. Use Retry.");
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static List<string> ReadStringList(JsonElement element, string name)
    {
        var list = new List<string>();
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                list.Add(item.GetString()!);
            }
        }

        return list;
    }

    private static IReadOnlyList<string>? OptionalList(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : [value];
}

internal static class PdfTextHint
{
    public static string FromBytes(byte[] pdf)
    {
        if (pdf.Length == 0)
        {
            return "";
        }

        var builder = new StringBuilder();
        var run = new StringBuilder();
        foreach (var raw in pdf)
        {
            var c = (char)raw;
            if (c is >= ' ' and <= '~')
            {
                run.Append(c);
                continue;
            }

            if (run.Length >= 4)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(run);
            }

            run.Clear();
            if (builder.Length > 4000)
            {
                break;
            }
        }

        if (run.Length >= 4 && builder.Length < 4000)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(run);
        }

        return builder.Length > 4000 ? builder.ToString(0, 4000) : builder.ToString();
    }
}
