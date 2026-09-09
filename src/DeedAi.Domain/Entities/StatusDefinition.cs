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
    public string? MapsTo { get; set; }
    public string? Kind { get; set; }
    public bool? IsSeed { get; set; }

    public void CoalesceNullCatalogFields()
    {
        MapsTo = string.IsNullOrWhiteSpace(MapsTo) ? Code : MapsTo.Trim();
        Kind = StatusCatalog.NormalizeKind(Kind);
        IsSeed ??= false;
    }
}
