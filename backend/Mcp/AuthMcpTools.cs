using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Roadmap.Api.Auth;

namespace Roadmap.Api.Mcp;

/// <summary>
/// Lets an agent that already reaches /mcp obtain a bearer token for the REST API, so scripts
/// that need /api/* (CV download, application PATCH, CV attach) run in a fresh session without
/// the login password. Whoever can call /mcp already has the import/write tools, so this adds
/// no new trust boundary — which is also why /mcp itself should sit behind MCP_SECRET.
/// </summary>
[McpServerToolType]
public sealed class AuthMcpTools(TokenIssuer issuer)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    [McpServerTool(Name = "issue_api_token"), Description(
        "Mint a bearer token for the REST API (/api/*). Same token the password login issues, signed with " +
        "the server's JWT_SECRET. Use it as 'Authorization: Bearer <token>', or put it in " +
        "~/.config/roadmap/credentials.env as ROADMAP_TOKEN=<token> for the Finder job-apply / attach-cvs " +
        "scripts. Default lifetime 30 days, maximum 90. Never writes anything.")]
    public string IssueApiToken(
        [Description("Lifetime in days, 1–90. Default 30.")] int days = TokenIssuer.DefaultDays)
    {
        var (token, expiresAt) = issuer.Issue(days);
        return J(new
        {
            token,
            expiresAt,
            days = Math.Clamp(days, 1, TokenIssuer.MaxDays),
            usage = "Authorization: Bearer <token> on /api/*, or ROADMAP_TOKEN=<token> in ~/.config/roadmap/credentials.env",
        });
    }
}
