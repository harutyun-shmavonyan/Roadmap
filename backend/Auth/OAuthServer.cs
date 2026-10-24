using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Auth;

/// <summary>
/// OAuth 2.1 for the MCP door, as the MCP authorization spec lays it out, so a connector in
/// claude.ai or ChatGPT signs in by sending you to Roadmap rather than by carrying a secret.
///
/// The flow a client follows, each step from the one before:
/// 1. <c>/mcp</c> answers 401 with <c>WWW-Authenticate: Bearer resource_metadata=…</c>.
/// 2. <c>/.well-known/oauth-protected-resource/mcp</c> (RFC 9728) names this app as the
///    authorization server; <c>/.well-known/oauth-authorization-server</c> (RFC 8414) lists the
///    endpoints below.
/// 3. <c>/oauth/register</c> (RFC 7591): the client registers itself. Open, because registering
///    grants nothing — but only to redirect hosts on <see cref="DefaultRedirectHosts"/> (plus
///    <c>OAUTH_REDIRECT_HOSTS</c>) or loopback, so a code can only ever be sent back to Claude,
///    ChatGPT or a local tool, never to a site that tricked you into signing in.
/// 4. <c>/oauth/authorize</c>: the Roadmap sign-in page. The password is the same
///    <c>AUTH_PASSWORD</c> the app's login takes; PKCE (S256) is required.
/// 5. <c>/oauth/token</c>: code → access token (a JWT whose audience is the MCP resource, see
///    <see cref="TokenIssuer.IssueMcpAccess"/>) and a refresh token, rotated on every use.
///
/// The static <c>MCP_SECRET</c> keeps working beside this for headless callers (scheduled
/// scripts cannot stop to sign in); Program.cs accepts either on /mcp.
/// </summary>
public static class OAuthServer
{
    public const string Scope = "roadmap";
    /// <summary>How long a rotated refresh token is still honoured, for a client retrying a lost response.</summary>
    public const int RefreshGraceSeconds = 120;
    static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    /// <summary>Sliding: each refresh issues a new one, so a connector in use stays signed in.</summary>
    static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(90);
    const int MaxClients = 500;

    /// <summary>
    /// Where an authorization code may be sent. claude.ai's callback is
    /// https://claude.ai/api/mcp/auth_callback, ChatGPT's https://chatgpt.com/connector_platform_oauth_redirect;
    /// claude.com, chat.openai.com and platform.openai.com are their other domains. Loopback (any
    /// port, RFC 8252) is always allowed for Claude Code and other local tools.
    /// </summary>
    static readonly string[] DefaultRedirectHosts =
        ["claude.ai", "claude.com", "chatgpt.com", "chat.openai.com", "platform.openai.com"];

    /// <summary>
    /// <c>Password</c> null = <c>AUTH_PASSWORD</c> is unset, and the sign-in page refuses rather
    /// than accept the development default. <c>PublicUrl</c> overrides the base URL, which is
    /// otherwise taken from the request (Railway's proxy supplies X-Forwarded-Proto).
    /// </summary>
    public sealed record Settings(string? Password, string? PublicUrl, HashSet<string> RedirectHosts)
    {
        public static Settings FromEnvironment()
        {
            var hosts = DefaultRedirectHosts.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var h in (Environment.GetEnvironmentVariable("OAUTH_REDIRECT_HOSTS") ?? "")
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                hosts.Add(h);
            return new Settings(
                NullIfEmpty(Environment.GetEnvironmentVariable("AUTH_PASSWORD")),
                NullIfEmpty(Environment.GetEnvironmentVariable("PUBLIC_URL"))?.TrimEnd('/'),
                hosts);
        }
    }

    // ===== URLs =====

    public static string BaseUrl(HttpContext ctx, Settings s)
    {
        if (s.PublicUrl is not null) return s.PublicUrl;
        var req = ctx.Request;
        var proto = req.Headers["X-Forwarded-Proto"].ToString().Split(',')[0].Trim().ToLowerInvariant();
        if (proto is not ("http" or "https")) proto = req.Scheme;
        var host = req.Headers["X-Forwarded-Host"].ToString().Split(',')[0].Trim();
        if (host.Length == 0) host = req.Host.Value ?? "localhost";
        return $"{proto}://{host.ToLowerInvariant()}";
    }

    /// <summary>The MCP resource (RFC 8707): what access tokens are issued for and checked against.</summary>
    public static string Resource(HttpContext ctx, Settings s) => BaseUrl(ctx, s) + "/mcp";

    /// <summary>The 401 challenge for /mcp: tells the client where the sign-in starts (RFC 9728 §5.1).</summary>
    public static string Challenge(HttpContext ctx, Settings s, bool tokenPresented) =>
        $"Bearer {(tokenPresented ? "error=\"invalid_token\", " : "")}" +
        $"resource_metadata=\"{BaseUrl(ctx, s)}/.well-known/oauth-protected-resource/mcp\", scope=\"{Scope}\"";

    /// <summary>
    /// Any resource on this origin means this MCP server, bound to its canonical URL; a resource
    /// elsewhere is not ours to grant (null).
    /// </summary>
    static string? CanonicalResource(HttpContext ctx, Settings s, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)) return Resource(ctx, s);
        if (!Uri.TryCreate(requested, UriKind.Absolute, out var r)) return null;
        var mine = new Uri(BaseUrl(ctx, s));
        return string.Equals(r.Scheme, mine.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.Authority, mine.Authority, StringComparison.OrdinalIgnoreCase)
            ? Resource(ctx, s) : null;
    }

    // ===== Endpoints =====

    public static void MapOAuthEndpoints(this WebApplication app, Settings s, TokenIssuer issuer)
    {
        IResult ProtectedResource(HttpContext ctx) => Json(new Dictionary<string, object>
        {
            ["resource"] = Resource(ctx, s),
            ["authorization_servers"] = new[] { BaseUrl(ctx, s) },
            ["scopes_supported"] = new[] { Scope },
            ["bearer_methods_supported"] = new[] { "header" },
            ["resource_name"] = "Roadmap",
        });
        app.MapGet("/.well-known/oauth-protected-resource", ProtectedResource).AllowAnonymous();
        app.MapGet("/.well-known/oauth-protected-resource/mcp", ProtectedResource).AllowAnonymous();

        IResult ServerMetadata(HttpContext ctx)
        {
            var b = BaseUrl(ctx, s);
            return Json(new Dictionary<string, object>
            {
                ["issuer"] = b,
                ["authorization_endpoint"] = b + "/oauth/authorize",
                ["token_endpoint"] = b + "/oauth/token",
                ["registration_endpoint"] = b + "/oauth/register",
                ["response_types_supported"] = new[] { "code" },
                ["response_modes_supported"] = new[] { "query" },
                ["grant_types_supported"] = new[] { "authorization_code", "refresh_token" },
                ["code_challenge_methods_supported"] = new[] { "S256" },
                ["token_endpoint_auth_methods_supported"] = new[] { "none", "client_secret_post", "client_secret_basic" },
                ["scopes_supported"] = new[] { Scope },
                ["authorization_response_iss_parameter_supported"] = true,
            });
        }
        app.MapGet("/.well-known/oauth-authorization-server", ServerMetadata).AllowAnonymous();
        app.MapGet("/.well-known/oauth-authorization-server/mcp", ServerMetadata).AllowAnonymous();
        // Anything else under .well-known (openid-configuration among them) is a plain 404 — not
        // the SPA's index.html, which a discovering client would try to parse as metadata.
        app.MapGet("/.well-known/{**rest}", () => Results.NotFound()).AllowAnonymous();

        app.MapPost("/oauth/register", (HttpContext ctx, RoadmapDbContext db) => RegisterAsync(ctx, db, s)).AllowAnonymous();
        app.MapGet("/oauth/authorize", (HttpContext ctx, RoadmapDbContext db) =>
            AuthorizeAsync(ctx, db, s, AuthRequest.From(k => ctx.Request.Query[k].ToString()), null, null)).AllowAnonymous();
        app.MapPost("/oauth/authorize", async (HttpContext ctx, RoadmapDbContext db) =>
        {
            var f = await ctx.Request.ReadFormAsync();
            return await AuthorizeAsync(ctx, db, s, AuthRequest.From(k => f[k].ToString()),
                f["password"].ToString(), f["action"].ToString());
        }).AllowAnonymous();
        app.MapPost("/oauth/token", (HttpContext ctx, RoadmapDbContext db) => TokenAsync(ctx, db, s, issuer)).AllowAnonymous();
    }

    // ===== Registration (RFC 7591) =====

    static async Task<IResult> RegisterAsync(HttpContext ctx, RoadmapDbContext db, Settings s)
    {
        NoStore(ctx);
        JsonElement body;
        try { body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body); }
        catch (JsonException) { return RegistrationError("invalid_client_metadata", "The body must be a JSON object."); }
        if (body.ValueKind != JsonValueKind.Object)
            return RegistrationError("invalid_client_metadata", "The body must be a JSON object.");

        var redirects = body.TryGetProperty("redirect_uris", out var r) && r.ValueKind == JsonValueKind.Array
            ? r.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Distinct().ToList()
            : [];
        if (redirects.Count == 0) return RegistrationError("invalid_redirect_uri", "redirect_uris is required.");
        if (redirects.Count > 10) return RegistrationError("invalid_redirect_uri", "At most 10 redirect_uris.");
        // RFC 7591 §3.2.1 lets the server register less than was asked: a client listing an extra
        // callback for some other flow still gets the ones Roadmap allows. None allowed → refused.
        var allowed = redirects.Where(u => IsAllowedRedirect(u, s)).ToList();
        if (allowed.Count == 0)
            return RegistrationError("invalid_redirect_uri",
                $"Redirect URI not allowed: {redirects[0]}. Roadmap sends sign-ins back only to https on " +
                $"{string.Join(", ", s.RedirectHosts.Order())} or to a loopback address.");
        redirects = allowed;

        // RFC 7591 §2: the default is client_secret_basic. MCP clients usually ask for "none".
        var method = Str(body, "token_endpoint_auth_method") ?? "client_secret_basic";
        if (method is not ("none" or "client_secret_post" or "client_secret_basic"))
            return RegistrationError("invalid_client_metadata", $"Unsupported token_endpoint_auth_method: {method}.");
        if (body.TryGetProperty("grant_types", out var g) && g.ValueKind == JsonValueKind.Array
            && !g.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == "authorization_code"))
            return RegistrationError("invalid_client_metadata", "Only the authorization_code grant (with refresh_token) is offered.");

        // Clients that registered and never completed a sign-in are dropped after a day, so open
        // registration cannot pile rows up; the cap is the backstop.
        var dayAgo = DateTime.UtcNow.AddDays(-1);
        await db.OAuthClients.Where(c => c.LastUsedAt == null && c.CreatedAt < dayAgo).ExecuteDeleteAsync();
        if (await db.OAuthClients.CountAsync() >= MaxClients)
            return RegistrationError("invalid_client_metadata", "Too many registered clients.");

        var client = new OAuthClient
        {
            Id = RandomToken(16),
            Name = CleanName(Str(body, "client_name")),
            RedirectUris = redirects,
            TokenEndpointAuthMethod = method,
        };
        string? secret = null;
        if (method != "none") { secret = RandomToken(32); client.SecretHash = Hash(secret); }
        db.OAuthClients.Add(client);
        await db.SaveChangesAsync();

        var res = new Dictionary<string, object?>
        {
            ["client_id"] = client.Id,
            ["client_id_issued_at"] = new DateTimeOffset(client.CreatedAt).ToUnixTimeSeconds(),
            ["client_name"] = client.Name,
            ["redirect_uris"] = client.RedirectUris,
            ["grant_types"] = new[] { "authorization_code", "refresh_token" },
            ["response_types"] = new[] { "code" },
            ["token_endpoint_auth_method"] = method,
            ["scope"] = Scope,
        };
        if (secret is not null) { res["client_secret"] = secret; res["client_secret_expires_at"] = 0; }
        return Json(res, StatusCodes.Status201Created);
    }

    static bool IsAllowedRedirect(string uri, Settings s)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u) || u.Fragment.Length > 0) return false;
        if (IsLoopback(u)) return u.Scheme is "http" or "https";
        return u.Scheme == "https" && s.RedirectHosts.Contains(u.Host);
    }

    static bool IsLoopback(Uri u) => u.Host is "localhost" or "127.0.0.1" or "[::1]";

    /// <summary>
    /// Exact match against a registered URI — except that a loopback redirect may name any port
    /// (RFC 8252 §7.3), since a local tool picks a free one each time it runs.
    /// </summary>
    static bool RedirectMatches(OAuthClient c, string redirect)
    {
        if (c.RedirectUris.Contains(redirect)) return true;
        if (!Uri.TryCreate(redirect, UriKind.Absolute, out var r) || !IsLoopback(r) || r.Fragment.Length > 0) return false;
        return c.RedirectUris.Any(x => Uri.TryCreate(x, UriKind.Absolute, out var u) && IsLoopback(u)
            && u.Scheme == r.Scheme && u.Host == r.Host && u.AbsolutePath == r.AbsolutePath && u.Query == r.Query);
    }

    // ===== Authorization: the sign-in page =====

    sealed record AuthRequest(string? ResponseType, string? ClientId, string? RedirectUri, string? State,
        string? CodeChallenge, string? CodeChallengeMethod, string? Scope, string? Resource)
    {
        public static AuthRequest From(Func<string, string> get) => new(
            NullIfEmpty(get("response_type")), NullIfEmpty(get("client_id")), NullIfEmpty(get("redirect_uri")),
            NullIfEmpty(get("state")), NullIfEmpty(get("code_challenge")), NullIfEmpty(get("code_challenge_method")),
            NullIfEmpty(get("scope")), NullIfEmpty(get("resource")));

        public IEnumerable<(string Name, string Value)> Fields()
        {
            (string, string?)[] all = [("response_type", ResponseType), ("client_id", ClientId), ("redirect_uri", RedirectUri),
                ("state", State), ("code_challenge", CodeChallenge), ("code_challenge_method", CodeChallengeMethod),
                ("scope", Scope), ("resource", Resource)];
            return all.Where(f => f.Item2 is not null).Select(f => (f.Item1, f.Item2!));
        }
    }

    /// <param name="action">null = show the page (GET); "allow" or "deny" = the form was submitted.</param>
    static async Task<IResult> AuthorizeAsync(HttpContext ctx, RoadmapDbContext db, Settings s,
        AuthRequest q, string? password, string? action)
    {
        // Until the client and its redirect check out, nothing can be sent back to it: an error
        // here is shown on the page, never redirected (RFC 6749 §4.1.2.1).
        var client = q.ClientId is null ? null : await db.OAuthClients.FindAsync(q.ClientId);
        if (client is null)
            return Page(ErrorBody("This app isn't registered with Roadmap. Remove the connector and add it again."), 400);
        var redirect = q.RedirectUri ?? (client.RedirectUris.Count == 1 ? client.RedirectUris[0] : null);
        if (redirect is null || !RedirectMatches(client, redirect))
            return Page(ErrorBody("The address this sign-in would return to doesn't match the one the app registered."), 400);
        if (s.Password is null)
            return Page(ErrorBody("Sign-in is off: AUTH_PASSWORD isn't set on the server."), 503);

        if (q.ResponseType != "code")
            return RedirectBack(ctx, redirect, q.State, BaseUrl(ctx, s), ("error", "unsupported_response_type"));
        if (q.CodeChallenge is null || (q.CodeChallengeMethod ?? "S256") != "S256")
            return RedirectBack(ctx, redirect, q.State, BaseUrl(ctx, s), ("error", "invalid_request"),
                ("error_description", "PKCE with code_challenge_method=S256 is required."));
        var resource = CanonicalResource(ctx, s, q.Resource);
        if (resource is null)
            return RedirectBack(ctx, redirect, q.State, BaseUrl(ctx, s), ("error", "invalid_target"));

        if (action is null) return Page(SignInBody(client, redirect, q, null));
        if (action != "allow")
            return RedirectBack(ctx, redirect, q.State, BaseUrl(ctx, s), ("error", "access_denied"));

        if (LockedFor() is TimeSpan wait)
            return Page(SignInBody(client, redirect, q,
                $"Too many wrong passwords. Try again in {Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes))} min."), 429);
        if (!FixedTimeEquals(password, s.Password))
        {
            Failures.Enqueue(DateTime.UtcNow);
            await Task.Delay(500);
            return Page(SignInBody(client, redirect, q, "Wrong password"), 401);
        }

        var code = RandomToken(32);
        db.OAuthAuthorizationCodes.Add(new OAuthAuthorizationCode
        {
            CodeHash = Hash(code), ClientId = client.Id, RedirectUri = redirect, CodeChallenge = q.CodeChallenge,
            Resource = resource, Scope = Scope, ExpiresAt = DateTime.UtcNow.Add(CodeLifetime),
        });
        await db.SaveChangesAsync();
        return RedirectBack(ctx, redirect, q.State, BaseUrl(ctx, s), ("code", code));
    }

    // Wrong passwords across all clients: ten in fifteen minutes closes the page for the rest of
    // the window. Global rather than per address, since X-Forwarded-For is the caller's to forge.
    static readonly ConcurrentQueue<DateTime> Failures = new();
    const int MaxFailures = 10;
    static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    static TimeSpan? LockedFor()
    {
        var now = DateTime.UtcNow;
        while (Failures.TryPeek(out var t) && now - t > FailureWindow) Failures.TryDequeue(out _);
        if (Failures.Count < MaxFailures || !Failures.TryPeek(out var oldest)) return null;
        return oldest + FailureWindow - now;
    }

    static IResult RedirectBack(HttpContext ctx, string redirect, string? state, string issuer,
        params (string Key, string Value)[] ps)
    {
        var sb = new StringBuilder(redirect).Append(redirect.Contains('?') ? '&' : '?');
        var all = ps.ToList();
        if (state is not null) all.Add(("state", state));
        all.Add(("iss", issuer)); // RFC 9207, advertised in the metadata
        sb.AppendJoin('&', all.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        // After a POST, 303 so the browser follows with a GET.
        ctx.Response.Headers.Location = sb.ToString();
        return Results.StatusCode(HttpMethods.IsPost(ctx.Request.Method) ? 303 : 302);
    }

    // ===== Token endpoint =====

    static async Task<IResult> TokenAsync(HttpContext ctx, RoadmapDbContext db, Settings s, TokenIssuer issuer)
    {
        NoStore(ctx);
        if (!ctx.Request.HasFormContentType)
            return TokenError("invalid_request", "Send the request as application/x-www-form-urlencoded.");
        var f = await ctx.Request.ReadFormAsync();

        var (clientId, secret) = ClientCredentials(ctx.Request, f);
        var client = clientId is null ? null : await db.OAuthClients.FindAsync(clientId);
        if (client is null) return TokenError("invalid_client", "Unknown client.", 401);
        if (client.SecretHash is not null && (secret is null || !FixedTimeEquals(Hash(secret), client.SecretHash)))
            return TokenError("invalid_client", "Client authentication failed.", 401);

        var requested = NullIfEmpty(f["resource"].ToString());
        if (requested is not null && CanonicalResource(ctx, s, requested) is null)
            return TokenError("invalid_target", "That resource isn't served here.");
        var now = DateTime.UtcNow;

        switch (f["grant_type"].ToString())
        {
            case "authorization_code":
            {
                var raw = f["code"].ToString();
                var code = raw.Length == 0 ? null : await db.OAuthAuthorizationCodes.FindAsync(Hash(raw));
                if (code is null || code.ClientId != client.Id || code.ExpiresAt < now)
                    return TokenError("invalid_grant", "The code is unknown, used or expired.");
                var redirect = NullIfEmpty(f["redirect_uri"].ToString());
                if (redirect is not null && redirect != code.RedirectUri)
                    return TokenError("invalid_grant", "redirect_uri differs from the authorization request.");
                if (!PkceMatches(f["code_verifier"].ToString(), code.CodeChallenge))
                    return TokenError("invalid_grant", "code_verifier doesn't match the code_challenge.");
                db.OAuthAuthorizationCodes.Remove(code); // single use, removed in the same save that issues
                return await IssueAsync(db, issuer, client, code.Resource, code.Scope);
            }
            case "refresh_token":
            {
                var raw = f["refresh_token"].ToString();
                var rt = raw.Length == 0 ? null : await db.OAuthRefreshTokens.FindAsync(Hash(raw));
                if (rt is null || rt.ClientId != client.Id || rt.ExpiresAt < now
                    || (rt.ReplacedAt is DateTime replaced && now - replaced > TimeSpan.FromSeconds(RefreshGraceSeconds)))
                    return TokenError("invalid_grant", "The refresh token is unknown, expired or already used.");
                rt.ReplacedAt ??= now;
                return await IssueAsync(db, issuer, client, rt.Resource, rt.Scope);
            }
            default:
                return TokenError("unsupported_grant_type", "Use authorization_code or refresh_token.");
        }
    }

    static async Task<IResult> IssueAsync(RoadmapDbContext db, TokenIssuer issuer, OAuthClient client,
        string resource, string scope)
    {
        var now = DateTime.UtcNow;
        var graceEdge = now.AddSeconds(-RefreshGraceSeconds);
        await db.OAuthAuthorizationCodes.Where(c => c.ExpiresAt < now).ExecuteDeleteAsync();
        await db.OAuthRefreshTokens.Where(t => t.ExpiresAt < now || (t.ReplacedAt != null && t.ReplacedAt < graceEdge))
            .ExecuteDeleteAsync();

        var refresh = RandomToken(32);
        db.OAuthRefreshTokens.Add(new OAuthRefreshToken
        {
            TokenHash = Hash(refresh), ClientId = client.Id, Resource = resource, Scope = scope,
            ExpiresAt = now.Add(RefreshLifetime),
        });
        client.LastUsedAt = now;
        await db.SaveChangesAsync();

        return Json(new Dictionary<string, object>
        {
            ["access_token"] = issuer.IssueMcpAccess(resource, client.Id, scope),
            ["token_type"] = "Bearer",
            ["expires_in"] = (int)TokenIssuer.McpAccessLifetime.TotalSeconds,
            ["refresh_token"] = refresh,
            ["scope"] = scope,
        });
    }

    /// <summary>client_secret_basic first (RFC 6749 §2.3.1, form-encoded inside the Basic pair), then the form.</summary>
    static (string? Id, string? Secret) ClientCredentials(HttpRequest req, IFormCollection f)
    {
        var auth = req.Headers.Authorization.ToString();
        if (auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var pair = Encoding.UTF8.GetString(Convert.FromBase64String(auth[6..].Trim()));
                var i = pair.IndexOf(':');
                if (i > 0)
                    return (WebUtility.UrlDecode(pair[..i]), WebUtility.UrlDecode(pair[(i + 1)..]));
            }
            catch (FormatException) { }
        }
        return (NullIfEmpty(f["client_id"].ToString()), NullIfEmpty(f["client_secret"].ToString()));
    }

    static bool PkceMatches(string verifier, string challenge)
    {
        if (verifier.Length is < 43 or > 128) return false;
        var computed = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return FixedTimeEquals(computed, challenge);
    }

    // ===== Responses =====

    static readonly JsonSerializerOptions JsonOpts = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    static IResult Json(object body, int status = 200) => Results.Json(body, JsonOpts, statusCode: status);

    static IResult RegistrationError(string error, string description) =>
        Json(new Dictionary<string, string> { ["error"] = error, ["error_description"] = description }, 400);

    static IResult TokenError(string error, string description, int status = 400) =>
        Json(new Dictionary<string, string> { ["error"] = error, ["error_description"] = description }, status);

    static void NoStore(HttpContext ctx)
    {
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.Headers.Pragma = "no-cache";
    }

    // ===== The page =====

    static IResult Page(string body, int status = 200) => new PageResult(body, status);

    sealed class PageResult(string body, int status) : IResult
    {
        public Task ExecuteAsync(HttpContext ctx)
        {
            var h = ctx.Response.Headers;
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            h.CacheControl = "no-store";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline' https://fonts.googleapis.com; " +
                                      "font-src https://fonts.gstatic.com; frame-ancestors 'none'; base-uri 'none'";
            return ctx.Response.WriteAsync(Shell(body));
        }
    }

    static string SignInBody(OAuthClient client, string redirect, AuthRequest q, string? error)
    {
        var host = new Uri(redirect).Authority;
        var name = client.Name ?? "An app";
        var hidden = string.Concat(q.Fields().Select(f =>
            $"<input type=\"hidden\" name=\"{f.Name}\" value=\"{H(f.Value)}\">"));
        return $"""
            <p><strong>{H(name)}</strong> wants access to your Roadmap. It will be able to read and change everything in it, the same as you can when signed in.</p>
            <p class="dest">After you allow it you go back to <strong>{H(host)}</strong>.</p>
            <form method="post" action="/oauth/authorize">
              {hidden}
              <label for="pw" class="sr">Roadmap password</label>
              <input id="pw" type="password" name="password" placeholder="Roadmap password" autocomplete="current-password" autofocus{(error is null ? "" : " aria-invalid=\"true\" aria-describedby=\"err\"")}>
              {(error is null ? "" : $"<div class=\"err\" id=\"err\" role=\"alert\">{H(error)}</div>")}
              <div class="row">
                <button class="primary" type="submit" name="action" value="allow">Allow</button>
                <button type="submit" name="action" value="deny" formnovalidate>Cancel</button>
              </div>
            </form>
            """;
    }

    static string ErrorBody(string message) => $"<p role=\"alert\">{H(message)}</p>";

    // The app's own login card, in its tokens (styles.css): light by default, the dark theme when
    // the system asks for it. The Allow button comes first in the markup so Enter in the password
    // field allows; row-reverse puts it on the right.
    static string Shell(string body) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Sign in · Roadmap</title>
        <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=DM+Sans:opsz,wght@9..40,400..700&display=swap">
        <style>
          :root { --bg: #ffffff; --card: #f8f9fa; --field: #ffffff; --border: #dadce0; --text: #202124;
                  --muted: #5f6368; --accent: #1a73e8; --on-accent: #ffffff; --danger: #d93025; color-scheme: light; }
          @media (prefers-color-scheme: dark) {
            :root { --bg: #0a0a0c; --card: #111114; --field: #0a0a0c; --border: #2a2a30; --text: #e8e6e3;
                    --muted: #8a8a8f; --accent: #c9a84c; --on-accent: #0a0a0c; --danger: #f28b82; color-scheme: dark; }
          }
          * { box-sizing: border-box; }
          body { margin: 0; min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 16px;
                 background: var(--bg); color: var(--text); font: 15px/1.5 'DM Sans', 'Google Sans', system-ui, sans-serif; }
          main { width: 100%; max-width: 360px; padding: 32px; background: var(--card); border: 1px solid var(--border); border-radius: 16px; }
          h1 { font-size: 24px; font-weight: 700; margin: 0 0 20px; }
          p { margin: 0 0 12px; color: var(--muted); }
          p strong { color: var(--text); font-weight: 600; }
          .dest { margin-bottom: 20px; }
          input[type=password] { width: 100%; font: inherit; font-size: 16px; padding: 12px 14px; margin-bottom: 12px;
                 background: var(--field); color: var(--text); border: 1px solid var(--border); border-radius: 8px; }
          input[type=password]:focus { outline: 2px solid var(--accent); outline-offset: 1px; border-color: var(--accent); }
          input[aria-invalid=true] { border-color: var(--danger); }
          .err { color: var(--danger); font-size: 13px; margin: -4px 0 12px; }
          .row { display: flex; flex-direction: row-reverse; gap: 8px; }
          button { flex: 1; font: inherit; font-weight: 600; padding: 12px; border-radius: 8px; cursor: pointer;
                   border: 1px solid var(--border); background: transparent; color: var(--text); }
          button:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
          button.primary { background: var(--accent); border-color: var(--accent); color: var(--on-accent); }
          .sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
        </style>
        </head>
        <body>
        <main>
          <h1>Roadmap</h1>
          {{body}}
        </main>
        </body>
        </html>
        """;

    // ===== Small helpers =====

    static string H(string s) => WebUtility.HtmlEncode(s);

    static string? Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? NullIfEmpty(v.GetString()) : null;

    static string? CleanName(string? name)
    {
        if (name is null) return null;
        var clean = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length == 0 ? null : clean.Length > 100 ? clean[..100] : clean;
    }

    static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    static string RandomToken(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    static bool FixedTimeEquals(string? a, string? b)
    {
        if (a is null || b is null) return false;
        var x = Encoding.UTF8.GetBytes(a);
        var y = Encoding.UTF8.GetBytes(b);
        return x.Length == y.Length && CryptographicOperations.FixedTimeEquals(x, y);
    }
}
