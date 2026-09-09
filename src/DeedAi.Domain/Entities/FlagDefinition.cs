namespace DeedAi.Domain.Entities;

public sealed class FlagDefinition
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Color { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
