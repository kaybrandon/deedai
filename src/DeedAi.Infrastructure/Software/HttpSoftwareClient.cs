using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DeedAi.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeedAi.Infrastructure.Software;

public sealed class SoftwareOptions
{
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
}

public sealed class HttpSoftwareClient(
    IHttpClientFactory httpFactory,
    IOptions<SoftwareOptions> options,
    ILogger<HttpSoftwareClient> logger) : ISoftwareClient
{
    public async Task<SoftwareLookupResult?> LookupAsync(string parcelId, string? clientName, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var url = $"lookup?parcelId={Uri.EscapeDataString(parcelId)}";
        if (!string.IsNullOrWhiteSpace(clientName))
        {
            url += $"&client={Uri.EscapeDataString(clientName)}";
        }

        var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = doc.RootElement;
        var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("extra", out var extraEl) && extraEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in extraEl.EnumerateObject())
            {
                extra[prop.Name] = prop.Value.ToString();
            }
        }

        return new SoftwareLookupResult(
            Read(root, "parcelId") ?? parcelId,
            Read(root, "owner"),
            Read(root, "legalDescription"),
            Read(root, "address"),
            Read(root, "softwareRecordId"),
            extra);
    }

    public async Task<SoftwarePushResult> PushAsync(SoftwarePushRequest request, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("push", request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Software push failed ({Status}): {Body}", (int)response.StatusCode, body);
            return new SoftwarePushResult(false, null, string.IsNullOrWhiteSpace(body) ? "Software push failed." : body);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var id = doc.RootElement.TryGetProperty("softwareRecordId", out var rec) ? rec.GetString() : null;
            var message = doc.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() : "Pushed to Software.";
            return new SoftwarePushResult(true, id, message ?? "Pushed to Software.");
        }
        catch (JsonException)
        {
            return new SoftwarePushResult(true, null, "Pushed to Software.");
        }
    }

    private HttpClient CreateClient()
    {
        var client = httpFactory.CreateClient(nameof(HttpSoftwareClient));
        var baseUrl = options.Value.BaseUrl ?? throw new InvalidOperationException("Software:BaseUrl is required.");
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        }

        return client;
    }

    private static string? Read(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
