using System.Text.Json;

namespace DeedAi.Domain;

public static class PartyNames
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string> Normalize(IEnumerable<string>? parties, string? legacy = null)
    {
        var names = (parties ?? [])
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (names.Count == 0 && !string.IsNullOrWhiteSpace(legacy))
        {
            names.Add(legacy.Trim());
        }

        return names;
    }

    public static string? Primary(IEnumerable<string>? parties, string? legacy = null) =>
        Normalize(parties, legacy).FirstOrDefault();

    public static List<string> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<List<string>>(json, Json)).ToList();
        }
        catch (JsonException)
        {
            return Normalize(null, json).ToList();
        }
    }

    public static string ToJson(IEnumerable<string>? parties) =>
        JsonSerializer.Serialize(Normalize(parties), Json);
}
