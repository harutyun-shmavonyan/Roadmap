namespace Roadmap.Api.Entities;

/// <summary>
/// A client that registered itself through dynamic client registration (RFC 7591) — claude.ai,
/// ChatGPT, Claude Code each do this the first time they connect, so nothing has to be typed into
/// a connector's OAuth fields. Registration grants nothing by itself: every authorization still
/// needs the Roadmap password on the sign-in page.
/// </summary>
public class OAuthClient
{
    /// <summary>The client_id handed out at registration.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Self-declared, so the sign-in page shows it beside the redirect host, which is not.</summary>
    public string? Name { get; set; }
    public List<string> RedirectUris { get; set; } = [];
    /// <summary>none (public client, PKCE only), client_secret_post or client_secret_basic.</summary>
    public string TokenEndpointAuthMethod { get; set; } = "none";
    /// <summary>SHA-256 of the client_secret, for confidential clients; the secret itself is never stored.</summary>
    public string? SecretHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Set on every token issued; a client never used is cleared a day after registering.</summary>
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// An authorization code between the sign-in page and the token endpoint: single use, five
/// minutes, bound to the client, its redirect URI and the PKCE challenge. Stored by hash.
/// </summary>
public class OAuthAuthorizationCode
{
    public string CodeHash { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A refresh token, stored by hash. Each use rotates it (OAuth 2.1 requires that for public
/// clients); the replaced one stays good for <c>OAuthServer.RefreshGraceSeconds</c> so a client
/// whose refresh response was lost in transit can retry instead of being signed out.
/// </summary>
public class OAuthRefreshToken
{
    public string TokenHash { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReplacedAt { get; set; }
}
