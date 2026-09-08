namespace DeedAi.Domain.Entities;

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public required string Role { get; set; }
}
