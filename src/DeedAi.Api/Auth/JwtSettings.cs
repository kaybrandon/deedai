namespace DeedAi.Api.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "deedai";
    public string Audience { get; set; } = "deedai-spa";
    public string Key { get; set; } = "";
    public int ExpiresMinutes { get; set; } = 480;
}
