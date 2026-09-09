namespace DeedAi.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, string Email, string DisplayName, string Role);

public sealed record MeResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? FullName,
    string Role,
    IReadOnlyList<Guid> ClientIds,
    IReadOnlyList<ClientScopeItem> Clients,
    bool HasPhoto);

public sealed record ClientScopeItem(Guid Id, string Name);

public sealed record UpdateProfileRequest(
    string DisplayName,
    string? FullName,
    string? Password);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string Password);

public sealed record VerifyEmailRequest(string Token);
