namespace DeedAi.Api.Contracts;

public sealed record SoftwareSettingsResponse(
    bool PushEnabled,
    string? DefaultGroup,
    string? FieldDefaultsJson,
    bool KeyConfigured,
    IReadOnlyList<SoftwareClientConfigItem> ClientConfigs,
    IReadOnlyList<SalesTabCodeItem> SalesTabCodes);

public sealed record UpdateSoftwareSettingsRequest(
    bool PushEnabled,
    string? DefaultGroup,
    string? FieldDefaultsJson);

public sealed record SoftwareStatusResponse(
    string Mode,
    bool Connected,
    bool PushEnabled,
    string? DefaultGroup,
    DateTimeOffset? LastSyncAt,
    string? LastSyncStatus,
    string? LastFailReason,
    Guid? LastDocumentId,
    string? LastDocumentName,
    bool KeyConfigured,
    string? ConnectionUrl);

public sealed record SoftwareClientConfigItem(
    Guid ClientId,
    string ClientName,
    string? Vendor,
    string? ApiUrl,
    string? GroupCode,
    bool RemoveLeadingZeros,
    int DateLabelDepth,
    bool DisplaySalesTab,
    bool SendConsideration,
    decimal ConsiderationThreshold,
    bool ResetExemptions,
    bool ResetSupplementYear,
    bool ResetSalesLetter,
    bool ResetSalesTab,
    bool ResetAgents,
    bool ResetMortgageCodes,
    bool HasAnyReset);

public sealed record UpdateSoftwareClientConfigRequest(
    string? Vendor,
    string? ApiUrl,
    string? GroupCode,
    bool RemoveLeadingZeros,
    int DateLabelDepth,
    bool DisplaySalesTab,
    bool SendConsideration,
    decimal ConsiderationThreshold,
    bool ResetExemptions,
    bool ResetSupplementYear,
    bool ResetSalesLetter,
    bool ResetSalesTab,
    bool ResetAgents,
    bool ResetMortgageCodes);

public sealed record SalesTabCodeItem(
    Guid Id,
    Guid? ClientId,
    string? ClientName,
    string Code,
    string Label,
    decimal MinConsideration,
    decimal? MaxConsideration,
    bool IsActive,
    int SortOrder);

public sealed record UpsertSalesTabCodeRequest(
    Guid? ClientId,
    string Code,
    string Label,
    decimal MinConsideration,
    decimal? MaxConsideration,
    bool IsActive,
    int SortOrder);

public sealed record AssignSalesTabCodeRequest(string? Code);

public sealed record SalesPageResponse(
    bool DisplaySalesTab,
    decimal? ConsiderationThreshold,
    IReadOnlyList<SalesTabCodeItem> Codes,
    IReadOnlyList<SaleRow> Rows);

public sealed record SoftwareFieldMapItem(
    Guid Id,
    string DeedField,
    string SoftwareField,
    string? SoftwareGroup,
    Guid? ClientId,
    string? ClientName,
    string? DeedType,
    bool IsActive,
    int SortOrder);

public sealed record UpsertSoftwareFieldMapRequest(
    string DeedField,
    string SoftwareField,
    string? SoftwareGroup,
    Guid? ClientId,
    string? DeedType,
    bool IsActive,
    int SortOrder);

public sealed record PropertyDefaultItem(
    Guid Id,
    string Scope,
    Guid? ClientId,
    string? ClientName,
    string? DeedType,
    string FieldKey,
    string? DefaultValue);

public sealed record UpsertPropertyDefaultRequest(
    string Scope,
    Guid? ClientId,
    string? DeedType,
    string FieldKey,
    string? DefaultValue);

public sealed record ResetPropertyDefaultsRequest(string Scope, Guid? ClientId, string? DeedType);

public sealed record SaleRow(
    Guid Id,
    string Name,
    string Client,
    Guid ClientId,
    string? Grantor,
    string? Grantee,
    string? InstrumentDate,
    string? Consideration,
    string? ParcelId,
    string? SalesTabCode,
    string Status,
    string? ReviewStatus,
    DateTimeOffset UpdatedAt);

public sealed record PurgeDeletedResponse(int Count, string Message);
