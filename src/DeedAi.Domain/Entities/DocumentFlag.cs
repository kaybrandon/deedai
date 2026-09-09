namespace DeedAi.Domain.Entities;

public sealed class DocumentFlag
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public Guid FlagDefinitionId { get; set; }
    public FlagDefinition Flag { get; set; } = null!;
}
