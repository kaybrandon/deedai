namespace DeedAi.Domain.Entities;

public sealed class NotificationSettings
{
    public static readonly Guid SingletonId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;
    public bool Enabled { get; set; } = true;
    public bool NotifyUploader { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
