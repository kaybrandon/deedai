namespace DeedAi.Domain.Entities;

public sealed class PropertyDefault
{
    public Guid Id { get; set; }
    public required string Scope { get; set; }
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public string? DeedType { get; set; }
    public required string FieldKey { get; set; }
    public string? DefaultValue { get; set; }
}
