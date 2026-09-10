namespace DeedAi.Infrastructure.Ocr;

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string? Endpoint { get; set; }
    public string? Key { get; set; }
    public string? Deployment { get; set; }
    public string Model { get; set; } = Domain.AiExtractModels.Default;
    public string ApiVersion { get; set; } = "2024-10-21";
    public string Mode { get; set; } = "";
    public bool AllowPricierModel { get; set; }
}
