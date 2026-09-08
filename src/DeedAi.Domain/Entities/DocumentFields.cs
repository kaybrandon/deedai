namespace DeedAi.Domain.Entities;

public sealed class DocumentFields
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public string? Grantor { get; set; }
    public string? Grantee { get; set; }
    public string? InstrumentDate { get; set; }
    public string? Consideration { get; set; }
    public string? ParcelId { get; set; }
    public string? Client { get; set; }
    public string? Notes { get; set; }
    public bool IsDraft { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
