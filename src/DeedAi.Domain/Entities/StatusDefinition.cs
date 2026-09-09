namespace DeedAi.Domain.Entities;

public sealed class StatusDefinition
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string DisplayName { get; set; }
    public required string Color { get; set; }
    public bool IsSystem { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
