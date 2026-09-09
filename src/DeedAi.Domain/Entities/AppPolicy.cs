namespace DeedAi.Domain.Entities;

public sealed class AppPolicy
{
    public static readonly Guid SingletonId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;
    public bool SoftwarePushEnabled { get; set; } = true;
    public string? SoftwareDefaultGroup { get; set; }
    public string? SoftwareFieldDefaultsJson { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
