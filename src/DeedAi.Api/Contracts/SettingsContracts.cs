namespace DeedAi.Api.Contracts;

public sealed record FlagItem(Guid Id, string Name, string Color, int SortOrder, bool IsActive);

public sealed record UpsertFlagRequest(string Name, string Color, int SortOrder, bool IsActive);

public sealed record StatusItem(Guid Id, string Code, string DisplayName, string Color, bool IsSystem, int SortOrder, bool IsActive);

public sealed record UpsertStatusRequest(string Code, string DisplayName, string Color, int SortOrder, bool IsActive);

public sealed record DeedTypeItem(Guid Id, string DeedType, string SoftwareCode, string? FieldMapJson, bool IsActive);

public sealed record UpsertDeedTypeRequest(string DeedType, string SoftwareCode, string? FieldMapJson, bool IsActive);

public sealed record SettingsExport(IReadOnlyList<FlagItem> Flags, IReadOnlyList<StatusItem> Statuses, IReadOnlyList<DeedTypeItem> DeedTypes);
