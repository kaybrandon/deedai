namespace DeedAi.Api.Contracts;

public sealed record ClientResponse(Guid Id, string Name);

public sealed record UserSummary(Guid Id, string DisplayName, string Role);

public sealed record DocumentListItem(
    Guid Id,
    string Name,
    string Client,
    Guid ClientId,
    string Status,
    DateTimeOffset UpdatedAt,
    string? Assignee,
    Guid? AssigneeUserId,
    bool CanRetry,
    bool IsDeleted);

public sealed record DocumentDetail(
    Guid Id,
    string Name,
    string Client,
    Guid ClientId,
    string Status,
    DateTimeOffset UpdatedAt,
    string? Assignee,
    Guid? AssigneeUserId,
    string? ErrorMessage,
    string? DiRawBlobPath,
    FieldDraft Fields,
    Guid? PreviousId,
    Guid? NextId);

public sealed record FieldDraft(
    string? Grantor,
    string? Grantee,
    string? InstrumentDate,
    string? Consideration,
    string? ParcelId,
    string? Client,
    string? Notes,
    bool IsDraft);

public sealed record FieldUpdateRequest(
    string? Grantor,
    string? Grantee,
    string? InstrumentDate,
    string? Consideration,
    string? ParcelId,
    string? Client,
    string? Notes,
    bool IsDraft);

public sealed record DashboardCounts(
    int Uploaded,
    int Queued,
    int Processing,
    int Ready,
    int Failed);

public sealed record UploadResult(int Queued, IReadOnlyList<DocumentListItem> Documents, IReadOnlyList<string> Errors);
