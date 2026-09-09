namespace DeedAi.Api.Contracts;

public sealed record ClientResponse(Guid Id, string Name, bool IsActive = true);

public sealed record UserSummary(Guid Id, string DisplayName, string Role);

public sealed record FlagSummary(Guid Id, string Name, string Color);

public sealed record LinkedDocument(Guid Id, string Name, string? Note);

public sealed record TeamMember(Guid Id, string DisplayName, string Role);

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
    bool IsDeleted,
    string? DeedType,
    string? ReviewStatus,
    IReadOnlyList<FlagSummary> Flags,
    string? ErrorMessage,
    string DisplayStatus);

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
    string? DeedType,
    string? ReviewStatus,
    FieldDraft Fields,
    Guid? PreviousId,
    Guid? NextId,
    IReadOnlyList<FlagSummary> Flags,
    IReadOnlyList<TeamMember> Team,
    IReadOnlyList<LinkedDocument> LinkedDocuments,
    DateTimeOffset? LastSoftwareSyncAt,
    string? LastSoftwareSyncStatus,
    string? LastSoftwareSyncDirection,
    string? LastSoftwareSyncFailReason,
    string? SoftwareRecordId,
    string DisplayStatus);

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
    bool IsDraft,
    string? DeedType,
    string? ReviewStatus);

public sealed record DashboardCounts(
    int Uploaded,
    int Queued,
    int Processing,
    int Ready,
    int Failed);

public sealed record DashboardStatusSlice(
    string Status,
    string Label,
    int Count,
    string? Color);

public sealed record DashboardStatusMix(
    int Total,
    IReadOnlyList<DashboardStatusSlice> Series);

public sealed record DashboardUserColumn(
    Guid? UserId,
    string DisplayName);

public sealed record DashboardStackedSeries(
    string Key,
    string Label,
    IReadOnlyList<int> Data,
    string? Color);

public sealed record DashboardByUser(
    IReadOnlyList<string> Labels,
    IReadOnlyList<DashboardUserColumn> Users,
    IReadOnlyList<DashboardStackedSeries> Series);

public sealed record DashboardVolume(
    IReadOnlyList<string> Labels,
    IReadOnlyList<DashboardStackedSeries> Series);

public sealed record UploadResult(int Queued, IReadOnlyList<DocumentListItem> Documents, IReadOnlyList<string> Errors);

public sealed record AssignRequest(Guid? AssigneeUserId);

public sealed record BulkAssignRequest(IReadOnlyList<Guid> DocumentIds, Guid? AssigneeUserId);

public sealed record SetFlagsRequest(IReadOnlyList<Guid> FlagIds);

public sealed record LinkDocumentRequest(Guid TargetDocumentId, string? Note);

public sealed record TeamMemberRequest(Guid UserId);
