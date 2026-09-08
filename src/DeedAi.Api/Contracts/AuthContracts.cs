namespace DeedAi.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string Token, string Email, string DisplayName, string Role);

public sealed record MeResponse(Guid Id, string Email, string DisplayName, string Role);
