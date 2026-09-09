namespace DeedAi.Api.Contracts;

public sealed record SoftwareSettingsResponse(
    bool PushEnabled,
    string? DefaultGroup,
    string? FieldDefaultsJson);

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
    string? LastDocumentName);

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
    string Status,
    string? ReviewStatus,
    DateTimeOffset UpdatedAt);

public sealed record PurgeDeletedResponse(int Count, string Message);
