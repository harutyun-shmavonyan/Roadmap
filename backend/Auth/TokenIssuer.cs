using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Roadmap.Api.Auth;

/// <summary>
/// Mints the app's bearer tokens. One signing key, shared by the password login and the
/// <c>issue_api_token</c> MCP tool, so a token from either is the same thing to every /api route.
/// </summary>
public sealed class TokenIssuer(string jwtSecret)
{
    public const int DefaultDays = 30;
    public const int MaxDays = 90;

    public SymmetricSecurityKey Key { get; } = new(Encoding.UTF8.GetBytes(jwtSecret));

    public (string Token, DateTime ExpiresAt) Issue(int days = DefaultDays)
    {
        days = Math.Clamp(days, 1, MaxDays);
        var expires = DateTime.UtcNow.AddDays(days);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, "owner")]),
            Expires = expires,
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256Signature)
        };
        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(handler.CreateToken(descriptor)), expires);
    }
}
