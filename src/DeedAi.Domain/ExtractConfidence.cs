using System.Text.Json;

namespace DeedAi.Domain;

public static class ExtractConfidence
{
    public const string High = "High";
    public const string Med = "Med";
    public const string Low = "Low";
    public const string Edited = "Edited";

    public static string Chip(double? score) =>
        score switch
        {
            null => "",
            >= 0.85 => High,
            >= 0.60 => Med,
            _ => Low
        };

    public static Dictionary<string, double> Parse(string? json)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetDouble(out var score))
                {
                    result[property.Name] = score;
                }
            }
        }
        catch (JsonException)
        {
            return result;
        }

        return result;
    }

    public static string ToJson(IReadOnlyDictionary<string, double>? scores)
    {
        if (scores is null || scores.Count == 0)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(scores);
    }
}
