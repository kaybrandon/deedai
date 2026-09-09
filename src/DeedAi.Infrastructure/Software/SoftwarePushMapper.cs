using System.Globalization;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Infrastructure.Software;

public static class SoftwarePushMapper
{
    public static async Task<AppPolicy> EnsurePolicyAsync(DeedAiDbContext db, CancellationToken cancellationToken)
    {
        var policy = await db.AppPolicies.FirstOrDefaultAsync(cancellationToken);
        if (policy is not null)
        {
            return policy;
        }

        policy = new AppPolicy
        {
            Id = AppPolicy.SingletonId,
            SoftwarePushEnabled = true,
            SoftwareDefaultGroup = "Property",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AppPolicies.Add(policy);
        await db.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public static async Task<Dictionary<string, string>> BuildMappedFieldsAsync(
        DeedAiDbContext db,
        Document document,
        DeedTypeMap? deedTypeMap,
        AppPolicy policy,
        SoftwareClientConfig? clientConfig,
        IReadOnlyList<SalesTabCode> salesCodes,
        CancellationToken cancellationToken)
    {
        var values = ResolveFieldValues(document, policy, clientConfig);
        var defaults = await db.PropertyDefaults.AsNoTracking().ToListAsync(cancellationToken);
        ApplyPropertyDefaults(values, defaults, document.ClientId, document.DeedType);
        var maps = await db.SoftwareFieldMaps.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in DeedFields.All)
        {
            if (!values.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var match = ChooseMap(maps, field, document.ClientId, document.DeedType);
            var key = match?.SoftwareField ?? MapFromDeedTypeJson(deedTypeMap, field) ?? field;
            var group = match?.SoftwareGroup
                        ?? (clientConfig?.GroupCode is { Length: > 0 } gc ? gc : policy.SoftwareDefaultGroup);
            if (!string.IsNullOrWhiteSpace(group))
            {
                mapped[$"{group}.{key}"] = value;
            }
            else
            {
                mapped[key] = value;
            }
        }

        ApplyClientConfigToMapped(mapped, document, clientConfig, salesCodes, values);
        return mapped;
    }

    public static Dictionary<string, string?> ResolveFieldValues(
        Document document,
        AppPolicy policy,
        SoftwareClientConfig? clientConfig = null)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [DeedFields.Grantor] = document.Fields?.Grantor,
            [DeedFields.Grantee] = document.Fields?.Grantee,
            [DeedFields.InstrumentDate] = document.Fields?.InstrumentDate,
            [DeedFields.Consideration] = document.Fields?.Consideration,
            [DeedFields.ParcelId] = document.Fields?.ParcelId,
            [DeedFields.Client] = document.Fields?.Client ?? document.Client.Name,
            [DeedFields.Notes] = document.Fields?.Notes
        };

        ApplyJsonDefaults(values, policy.SoftwareFieldDefaultsJson);
        ApplyClientConfigToValues(values, clientConfig);
        return values;
    }

    public static void ApplyClientConfigToValues(Dictionary<string, string?> values, SoftwareClientConfig? config)
    {
        if (config is null)
        {
            return;
        }

        if (config.RemoveLeadingZeros && values.TryGetValue(DeedFields.ParcelId, out var parcel))
        {
            values[DeedFields.ParcelId] = SalesTabRules.StripLeadingZeros(parcel);
        }

        if (!config.SendConsideration)
        {
            values[DeedFields.Consideration] = null;
        }
    }

    public static void ApplyClientConfigToMapped(
        Dictionary<string, string> mapped,
        Document document,
        SoftwareClientConfig? config,
        IReadOnlyList<SalesTabCode> salesCodes,
        IReadOnlyDictionary<string, string?> values)
    {
        if (config is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(config.Vendor))
        {
            mapped["Vendor"] = config.Vendor.Trim();
        }

        if (!string.IsNullOrWhiteSpace(config.ApiUrl))
        {
            mapped["ApiUrl"] = config.ApiUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(config.GroupCode))
        {
            mapped["GroupCode"] = config.GroupCode.Trim();
        }

        ApplyDateLabelDepth(mapped, document, config, values);

        if (config.ResetExemptions) mapped["Reset.Exemptions"] = "true";
        if (config.ResetSupplementYear) mapped["Reset.SupplementYear"] = "true";
        if (config.ResetSalesLetter) mapped["Reset.SalesLetter"] = "true";
        if (config.ResetSalesTab) mapped["Reset.SalesTab"] = "true";
        if (config.ResetAgents) mapped["Reset.Agents"] = "true";
        if (config.ResetMortgageCodes) mapped["Reset.MortgageCodes"] = "true";

        if (config.ResetSalesTab)
        {
            document.SalesTabCode = null;
            return;
        }

        var amount = SalesTabRules.ParseConsideration(document.Fields?.Consideration);
        if (!SalesTabRules.MeetsThreshold(config, amount) || amount is null)
        {
            return;
        }

        var match = SalesTabRules.Match(salesCodes, document.ClientId, amount.Value);
        if (match is null)
        {
            return;
        }

        document.SalesTabCode = match.Code;
        mapped["SalesTab.Code"] = match.Code;
        mapped["SalesTab.Label"] = match.Label;
    }

    public static void ApplyPropertyDefaults(
        Dictionary<string, string?> values,
        IEnumerable<PropertyDefault> defaults,
        Guid clientId,
        string? deedType)
    {
        foreach (var item in defaults)
        {
            if (!values.TryGetValue(item.FieldKey, out var current) || !string.IsNullOrWhiteSpace(current))
            {
                continue;
            }

            var matches = item.Scope == PropertyDefaultScopes.Client
                ? item.ClientId == clientId
                : item.Scope == PropertyDefaultScopes.DeedType
                  && !string.IsNullOrWhiteSpace(deedType)
                  && string.Equals(item.DeedType, deedType, StringComparison.OrdinalIgnoreCase);
            if (matches)
            {
                values[item.FieldKey] = item.DefaultValue;
            }
        }
    }

    private static void ApplyDateLabelDepth(
        Dictionary<string, string> mapped,
        Document document,
        SoftwareClientConfig config,
        IReadOnlyDictionary<string, string?> values)
    {
        var depth = Math.Clamp(config.DateLabelDepth <= 0 ? 1 : config.DateLabelDepth, 1, 3);
        if (depth >= 1)
        {
            var instrument = values.GetValueOrDefault(DeedFields.InstrumentDate) ?? document.Fields?.InstrumentDate;
            if (!string.IsNullOrWhiteSpace(instrument))
            {
                mapped["Date.Label1"] = instrument;
            }
        }

        if (depth >= 2)
        {
            mapped["Date.Label2"] = document.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (depth >= 3)
        {
            mapped["Date.Label3"] = document.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }

    private static SoftwareFieldMap? ChooseMap(
        IReadOnlyList<SoftwareFieldMap> maps,
        string deedField,
        Guid clientId,
        string? deedType)
    {
        return maps
            .Where(x => string.Equals(x.DeedField, deedField, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ClientId == clientId && MatchesDeedType(x, deedType) ? 3
                : x.ClientId == clientId ? 2
                : MatchesDeedType(x, deedType) ? 1
                : x.ClientId is null && string.IsNullOrWhiteSpace(x.DeedType) ? 0
                : -1)
            .FirstOrDefault(x =>
                (x.ClientId is null || x.ClientId == clientId)
                && (string.IsNullOrWhiteSpace(x.DeedType) || MatchesDeedType(x, deedType)));
    }

    private static bool MatchesDeedType(SoftwareFieldMap map, string? deedType) =>
        !string.IsNullOrWhiteSpace(map.DeedType)
        && string.Equals(map.DeedType, deedType, StringComparison.OrdinalIgnoreCase);

    private static string? MapFromDeedTypeJson(DeedTypeMap? map, string field)
    {
        if (map?.FieldMapJson is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(map.FieldMapJson);
            if (doc.RootElement.TryGetProperty(field, out var mappedName) && mappedName.ValueKind == JsonValueKind.String)
            {
                return mappedName.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static void ApplyJsonDefaults(Dictionary<string, string?> values, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!values.TryGetValue(property.Name, out var current) || !string.IsNullOrWhiteSpace(current))
                {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    values[property.Name] = property.Value.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // keep existing values
        }
    }
}
