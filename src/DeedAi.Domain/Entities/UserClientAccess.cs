namespace DeedAi.Domain.Entities;

public sealed class UserClientAccess
{
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
}
