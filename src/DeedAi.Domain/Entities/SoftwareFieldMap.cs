namespace DeedAi.Domain.Entities;

public sealed class SoftwareFieldMap
{
    public Guid Id { get; set; }
    public required string DeedField { get; set; }
    public required string SoftwareField { get; set; }
    public string? SoftwareGroup { get; set; }
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public string? DeedType { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
