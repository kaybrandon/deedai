namespace DeedAi.Domain.Entities;

public sealed class DocumentTeamMember
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
}
