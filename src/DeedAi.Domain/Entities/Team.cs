namespace DeedAi.Domain.Entities;

public sealed class Team
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<TeamUser> Members { get; set; } = new List<TeamUser>();
}
