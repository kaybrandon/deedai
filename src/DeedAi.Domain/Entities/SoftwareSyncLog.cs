namespace DeedAi.Domain.Entities;

public sealed class SoftwareSyncLog
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public required string Direction { get; set; }
    public required string Status { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
