namespace DeedAi.Domain.Entities;

public sealed class SoftwareImageCode
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public required string Code { get; set; }
    public required string Label { get; set; }
    public string? DeedType { get; set; }
    public bool UseOnLookup { get; set; } = true;
    public bool UseOnPush { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public void CoalesceNullFields()
    {
        Code ??= "";
        Label ??= "";
        DeedType ??= "";
    }
}
