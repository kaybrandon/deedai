namespace DeedAi.Domain.Entities;

public sealed class DeedTypeMap
{
    public Guid Id { get; set; }
    public required string DeedType { get; set; }
    public required string SoftwareCode { get; set; }
    public string? FieldMapJson { get; set; }
    public bool IsActive { get; set; } = true;
}
