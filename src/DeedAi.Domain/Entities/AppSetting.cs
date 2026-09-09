namespace DeedAi.Domain.Entities;

/// <summary>
/// Key/value row for runtime app flags. Swagger UI enablement is stored here
/// so it can change without an Azure App Setting or redeploy.
/// </summary>
public sealed class AppSetting
{
    public const string SwaggerEnabledKey = "Swagger.Enabled";
    public const string OcrWorkerHeartbeatKey = "Health.Ocr.Heartbeat";
    public const string OcrLastDequeueKey = "Health.Ocr.Dequeue";
    public const string DiLastSuccessKey = "Health.Di.Success";
    public const string DiLastFailKey = "Health.Di.Fail";

    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
