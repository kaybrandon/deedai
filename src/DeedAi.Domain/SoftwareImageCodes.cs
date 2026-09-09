using DeedAi.Domain.Entities;

namespace DeedAi.Domain;

public static class SoftwareImageCodes
{
    public static string? ForLookup(
        SoftwareClientConfig? config,
        IReadOnlyList<SoftwareImageCode>? codes,
        string? deedType)
    {
        if (!string.IsNullOrWhiteSpace(config?.LookupImageCode))
        {
            return config.LookupImageCode.Trim();
        }

        return Choose(codes, deedType, lookup: true);
    }

    public static string? ForPush(
        SoftwareClientConfig? config,
        IReadOnlyList<SoftwareImageCode>? codes,
        string? deedType)
    {
        if (!string.IsNullOrWhiteSpace(config?.PushImageCode))
        {
            return config.PushImageCode.Trim();
        }

        return Choose(codes, deedType, lookup: false);
    }

    private static string? Choose(IReadOnlyList<SoftwareImageCode>? codes, string? deedType, bool lookup)
    {
        if (codes is null || codes.Count == 0)
        {
            return null;
        }

        var active = codes
            .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Code))
            .Where(x => lookup ? x.UseOnLookup : x.UseOnPush)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToList();

        if (active.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(deedType))
        {
            var match = active.FirstOrDefault(x =>
                string.Equals(x.DeedType, deedType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Code, deedType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Label, deedType, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match.Code.Trim();
            }
        }

        return active[0].Code.Trim();
    }
}