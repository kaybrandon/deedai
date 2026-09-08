using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DeedAi.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DeedAi.Api.Auth;

public sealed class JwtTokenService(IOptions<JwtSettings> settings)
{
    public const string KeyId = "deedai";

    public static SymmetricSecurityKey CreateKey(string secret) =>
        new(Encoding.UTF8.GetBytes(secret)) { KeyId = KeyId };

    public string Create(UserAccount user)
    {
        var key = CreateKey(settings.Value.Key);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            settings.Value.Issuer,
            settings.Value.Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(settings.Value.ExpiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
