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

    /// <summary>Lifetime of an OAuth access token for /mcp; the client refreshes it after that.</summary>
    public static readonly TimeSpan McpAccessLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// An OAuth access token for the MCP door. Same principal and key as <see cref="Issue"/>, but its
    /// audience is the MCP resource (RFC 8707), and /mcp accepts nothing else: a token minted for
    /// some other resource is refused there, as the MCP authorization spec requires.
    /// </summary>
    public string IssueMcpAccess(string resource, string clientId, string scope)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name, "owner"),
                new Claim("client_id", clientId),
                new Claim("scope", scope)]),
            Audience = resource,
            Expires = DateTime.UtcNow.Add(McpAccessLifetime),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256Signature)
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    /// <summary>True when the token is one of ours, unexpired and issued for this MCP resource.</summary>
    public bool IsValidMcpAccess(string token, string resource)
    {
        try
        {
            new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = true,
                ValidAudience = resource,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = Key,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);
            return true;
        }
        catch (Exception) { return false; }
    }
}
