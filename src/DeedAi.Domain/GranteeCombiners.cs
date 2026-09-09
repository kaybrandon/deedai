namespace DeedAi.Domain;

/// <summary>
/// How multiple Grantee names combine for Software lookup and push.
/// Label is always Grantee — never CAMA.
/// </summary>
public static class GranteeCombiners
{
    public const string First = "first";
    public const string Last = "last";
    public const string And = "and";
    public const string Ampersand = "ampersand";
    public const string Semicolon = "semicolon";
    public const string Comma = "comma";

    public static readonly string[] All =
    [
        First, Last, And, Ampersand, Semicolon, Comma
    ];

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && All.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? value) =>
        IsKnown(value) ? value!.Trim().ToLowerInvariant() : First;

    public static string? Combine(IEnumerable<string>? parties, string? legacy, string? combiner)
    {
        var names = PartyNames.Normalize(parties, legacy);
        if (names.Count == 0)
        {
            return null;
        }

        return Normalize(combiner) switch
        {
            Last => names[^1],
            And => string.Join(" and ", names),
            Ampersand => string.Join(" & ", names),
            Semicolon => string.Join("; ", names),
            Comma => string.Join(", ", names),
            _ => names[0]
        };
    }
}
