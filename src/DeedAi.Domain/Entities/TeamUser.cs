namespace DeedAi.Domain.Entities;

public sealed class TeamUser
{
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
}
