namespace DeedAi.Domain.Entities;

public sealed class DocumentLink
{
    public Guid SourceDocumentId { get; set; }
    public Document Source { get; set; } = null!;
    public Guid TargetDocumentId { get; set; }
    public Document Target { get; set; } = null!;
    public string? Note { get; set; }
}
