using System.Globalization;
using DeedAi.Domain.Entities;

namespace DeedAi.Domain;

public static class SalesTabRules
{
    public static decimal? ParseConsideration(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = raw.Trim().Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal);
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static bool MeetsThreshold(SoftwareClientConfig? config, decimal? consideration) =>
        config is { DisplaySalesTab: true } && consideration is { } amount && amount >= config.ConsiderationThreshold;

    public static SalesTabCode? Match(IEnumerable<SalesTabCode> codes, Guid clientId, decimal consideration) =>
        codes
            .Where(x => x.IsActive && (x.ClientId is null || x.ClientId == clientId))
            .Where(x => consideration >= x.MinConsideration && (x.MaxConsideration is null || consideration <= x.MaxConsideration))
            .OrderByDescending(x => x.ClientId == clientId)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .FirstOrDefault();

    public static string? ResolveCode(
        string? persisted,
        IEnumerable<SalesTabCode> codes,
        Guid clientId,
        decimal? consideration)
    {
        if (!string.IsNullOrWhiteSpace(persisted))
        {
            return persisted;
        }

        if (consideration is not { } amount)
        {
            return null;
        }

        return Match(codes, clientId, amount)?.Code;
    }

    public static string? StripLeadingZeros(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        var stripped = trimmed.TrimStart('0');
        return stripped.Length == 0 ? "0" : stripped;
    }
}
