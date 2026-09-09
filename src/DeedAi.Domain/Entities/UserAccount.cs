namespace DeedAi.Domain.Entities;

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public string? FullName { get; set; }
    public string? PhotoBlobPath { get; set; }
    public required string Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool EmailVerified { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<UserClientAccess> ClientAccess { get; set; } = new List<UserClientAccess>();
}
