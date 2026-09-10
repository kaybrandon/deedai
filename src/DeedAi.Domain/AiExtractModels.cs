namespace DeedAi.Domain;

/// <summary>
/// Cheap Azure OpenAI deployments only. Escalate spend to Chief of Staff before
/// a pricier model. Do not raise quotas.
/// </summary>
public static class AiExtractModels
{
    public const string Default = "gpt-4o-mini";

    public static readonly string[] Cheap =
    [
        "gpt-4o-mini",
        "gpt-4o-mini-2024-07-18",
        "gpt-4.1-mini",
        "gpt-4.1-nano",
        "gpt-35-turbo",
        "gpt-3.5-turbo"
    ];

    public static bool IsCheap(string? name)
    {
        var normalized = Normalize(name);
        return Cheap.Any(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)
                                 || normalized.StartsWith(item + "-", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsPricier(string? name)
    {
        var normalized = Normalize(name);
        if (string.IsNullOrWhiteSpace(normalized) || IsCheap(normalized))
        {
            return false;
        }

        return normalized.Contains("gpt-4o", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("gpt-4.1", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("gpt-4-turbo", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("gpt-4", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("o3", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("o4", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("gpt-5", StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string? name) =>
        string.IsNullOrWhiteSpace(name) ? Default : name.Trim();

    public static string EscalateMessage(string? name) =>
        $"Escalate spend to Chief of Staff before using pricier model '{Normalize(name)}'. Default stays {Default}. Do not raise quotas.";
}
