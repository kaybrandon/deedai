namespace DeedAi.Domain.Entities;

public sealed class SessionSettings
{
    public static readonly Guid SingletonId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    public const int DefaultIdleTimeoutMinutes = 30;
    public const int MinIdleTimeoutMinutes = 5;
    public const int MaxIdleTimeoutMinutes = 1440;

    public Guid Id { get; set; } = SingletonId;
    public int IdleTimeoutMinutes { get; set; } = DefaultIdleTimeoutMinutes;
    public DateTimeOffset UpdatedAt { get; set; }

    public static int Clamp(int minutes) =>
        Math.Clamp(minutes, MinIdleTimeoutMinutes, MaxIdleTimeoutMinutes);
}
