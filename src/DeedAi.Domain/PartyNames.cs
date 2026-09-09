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

    /// <summary>
    /// Empty rows are allowed only as a single blank placeholder. Mixed empty + filled rows fail.
    /// </summary>
    public static string? EmptyRowsMessage(IEnumerable<string>? rows, string label)
    {
        if (rows is null)
        {
            return null;
        }

        var list = rows.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var empty = list.Count(string.IsNullOrWhiteSpace);
        if (empty == 0)
        {
            return null;
        }

        if (empty == list.Count && list.Count == 1)
        {
            return null;
        }

        return $"Fill or remove empty {label} rows.";
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
