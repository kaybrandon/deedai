namespace DeedAi.Api.Contracts;

public sealed record FlagItem(Guid Id, string Name, string Color, int SortOrder, bool IsActive);

public sealed record UpsertFlagRequest(string Name, string Color, int SortOrder, bool IsActive);

public sealed record StatusItem(Guid Id, string Code, string DisplayName, string Color, bool IsSystem, int SortOrder, bool IsActive);

public sealed record UpsertStatusRequest(string Code, string DisplayName, string Color, int SortOrder, bool IsActive);

public sealed record DeedTypeItem(Guid Id, string DeedType, string SoftwareCode, string? FieldMapJson, bool IsActive);

public sealed record UpsertDeedTypeRequest(string DeedType, string SoftwareCode, string? FieldMapJson, bool IsActive);

public sealed record SettingsExport(
    IReadOnlyList<FlagItem> Flags,
    IReadOnlyList<StatusItem> Statuses,
    IReadOnlyList<DeedTypeItem> DeedTypes,
    IReadOnlyList<TeamItem> Teams,
    IReadOnlyList<ClientItem> Clients,
    NotificationSettingsResponse Notifications);

public sealed record ClientItem(Guid Id, string Name, bool IsActive);

public sealed record UpsertClientRequest(string Name, bool IsActive);

public sealed record TeamMemberItem(Guid Id, string DisplayName, string Role, string Email);

public sealed record TeamItem(Guid Id, string Name, bool IsActive, IReadOnlyList<TeamMemberItem> Members);

public sealed record UpsertTeamRequest(string Name, bool IsActive, IReadOnlyList<Guid> UserIds);

public sealed record NotificationSettingsResponse(bool Enabled, bool NotifyUploader, IReadOnlyList<string> Events, string RecipientsSummary);

public sealed record UpdateNotificationSettingsRequest(bool Enabled, bool NotifyUploader);

public sealed record NotifyPreviewResponse(
    bool Enabled,
    bool NotifyUploader,
    IReadOnlyList<NotifyRecipientItem> Recipients,
    IReadOnlyList<string> Events);

public sealed record NotifyRecipientItem(string Email, string DisplayName, string Reason);

public sealed record SessionConfigResponse(int IdleTimeoutMinutes, int DefaultMinutes, string Source);

public sealed record UpdateSessionSettingsRequest(int IdleTimeoutMinutes);

public sealed record OcrCleanupItem(Guid Id, string Kind, string Value, bool IsActive, int SortOrder);

public sealed record UpsertOcrCleanupRequest(string Kind, string Value, bool IsActive, int SortOrder);

public sealed record SwaggerSettingResponse(bool Enabled);

public sealed record UpdateSwaggerSettingRequest(bool Enabled);

public sealed record EmailSettingsResponse(
    string Mode,
    bool Configured,
    bool SendGridConfigured,
    string? SendGridKeyLast4,
    bool SmtpHostConfigured,
    string? SmtpHost,
    bool SmtpPortConfigured,
    int? SmtpPort,
    bool? SmtpTls,
    bool SmtpUsernameConfigured,
    bool SmtpPasswordConfigured,
    int SmtpTimeoutSeconds,
    string FromName,
    string FromAddress,
    bool VerifyRequired,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailAt,
    string? LastFailReason);

public sealed record UpdateEmailSettingsRequest(
    string Mode,
    string? FromName,
    string? FromAddress,
    bool VerifyRequired);

public sealed record TestEmailRequest(string To);

public sealed record TestEmailResponse(bool Passed, string Message, DateTimeOffset At);
