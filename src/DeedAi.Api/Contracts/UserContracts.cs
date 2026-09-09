namespace DeedAi.Api.Contracts;

public sealed record UserDetail(
    Guid Id,
    string Email,
    string DisplayName,
    string? FullName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> ClientIds,
    bool HasPhoto);

public sealed record UpsertUserRequest(
    string Email,
    string DisplayName,
    string? FullName,
    string Role,
    string? Password,
    bool IsActive,
    IReadOnlyList<Guid> ClientIds);
