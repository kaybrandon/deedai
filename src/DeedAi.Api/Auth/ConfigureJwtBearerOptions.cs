using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DeedAi.Api.Auth;

public sealed class ConfigureJwtBearerOptions(IOptions<JwtSettings> settings) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        var jwt = settings.Value;
        options.RequireHttpsMetadata = false;
        options.IncludeErrorDetails = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = JwtTokenService.CreateKey(jwt.Key),
            TryAllIssuerSigningKeys = true,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name
        };
    }
}
