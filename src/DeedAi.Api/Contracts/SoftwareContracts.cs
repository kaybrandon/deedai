namespace DeedAi.Api.Contracts;

public sealed record SoftwareLookupResponse(
    string ParcelId,
    string? Owner,
    string? LegalDescription,
    string? Address,
    string? SoftwareRecordId,
    IReadOnlyDictionary<string, string> Extra);

public sealed record SoftwarePushResponse(
    bool Succeeded,
    string? SoftwareRecordId,
    string Message,
    DateTimeOffset? LastSyncAt,
    string? LastSyncStatus,
    string? FailReason);
