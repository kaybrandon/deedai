namespace DeedAi.Domain.Entities;

public sealed class Client
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
