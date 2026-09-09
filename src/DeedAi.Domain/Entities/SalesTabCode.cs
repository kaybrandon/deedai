namespace DeedAi.Domain.Entities;

public sealed class SalesTabCode
{
    public Guid Id { get; set; }
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public required string Code { get; set; }
    public required string Label { get; set; }
    public decimal MinConsideration { get; set; }
    public decimal? MaxConsideration { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
