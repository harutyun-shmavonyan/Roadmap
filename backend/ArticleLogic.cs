using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// Pure helpers shared by the Articles REST endpoints and MCP tools: reading-time
/// estimation, the "3 points per hour of reading" award, and DTO mapping.
/// </summary>
public static class ArticleLogic
{
    /// <summary>Points awarded for finishing an article: 3 points per hour of reading.</summary>
    public static double PointsFor(int readMinutes) => Math.Round(3.0 * readMinutes / 60.0, 2);

    /// <summary>
    /// Estimate reading time from word count at ~200 wpm; always at least 1 minute. When the body
    /// is HTML, tags are stripped first so markup doesn't inflate the count.
    /// </summary>
    public static int EstimateReadMinutes(string? content, string? format = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return 1;
        var text = string.Equals(format, "html", StringComparison.OrdinalIgnoreCase) ? StripHtml(content) : content;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        // 2× the raw ~200 wpm estimate (user-tuned pacing factor) — i.e. effectively ~100 wpm.
        return Math.Max(1, (int)Math.Round(words / 200.0)) * 2;
    }

    /// <summary>
    /// Reduce an uploaded filename to a safe reference name used in <c>{{img:NAME}}</c>: basename
    /// only (no directories), lowercased, keeping letters/digits/dot/dash/underscore and collapsing
    /// everything else to a dash. Returns "" when nothing usable remains.
    /// </summary>
    public static string SanitizeImageName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "";
        var baseName = Path.GetFileName(fileName.Trim());
        var chars = baseName.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? char.ToLowerInvariant(c) : '-').ToArray();
        return new string(chars).Trim('-');
    }

    /// <summary>Best-effort MIME type from a filename extension, defaulting to octet-stream.</summary>
    public static string GuessContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".avif" => "image/avif",
        ".bmp" => "image/bmp",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream",
    };

    /// <summary>Crudely strip HTML tags (and script/style bodies) to plain text for word counting.</summary>
    private static string StripHtml(string html)
    {
        var noBlocks = System.Text.RegularExpressions.Regex.Replace(
            html, "<(script|style)[^>]*>.*?</\\1>", " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        return System.Text.RegularExpressions.Regex.Replace(noBlocks, "<[^>]+>", " ");
    }

    public static ArticleSummaryDto ToSummary(Article a, int imageCount) => new(
        a.Id, a.Title, a.Format, a.ReadMinutes, PointsFor(a.ReadMinutes),
        a.IsRead, a.ReadOn?.ToString("yyyy-MM-dd"), a.SortOrder, imageCount, a.CreatedAt, a.UpdatedAt);

    public static ArticleDto ToDetail(Article a, IReadOnlyList<ArticleImageDto>? images = null) => new(
        a.Id, a.Title, a.Format, a.Content, a.ReadMinutes, PointsFor(a.ReadMinutes),
        a.IsRead, a.ReadOn?.ToString("yyyy-MM-dd"), a.SortOrder,
        images ?? Array.Empty<ArticleImageDto>(), a.ChatUrl, a.CreatedAt, a.UpdatedAt);

    /// <summary>Normalise an optional chat URL: trim, keep only http(s), else null.</summary>
    public static string? NormChatUrl(string? url)
    {
        var u = url?.Trim();
        if (string.IsNullOrEmpty(u)) return null;
        return Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? u : null;
    }

    /// <summary>
    /// Build a fully self-contained HTML document for an article: every <c>{{img:NAME}}</c>
    /// placeholder is replaced with a <c>data:</c> URI carrying the uploaded image bytes, so the
    /// result renders identically in the app's reader iframe and in a standalone "open in new tab"
    /// view with no further requests. If the author already supplied a full document (starts with
    /// <c>&lt;!doctype</c> or <c>&lt;html</c>) it is returned as-is; a bare fragment is wrapped in a
    /// minimal responsive shell.
    /// </summary>
    public static string BuildStandaloneHtml(Article a, IEnumerable<ArticleImage> images)
    {
        var html = a.Content ?? "";
        // Longest names first so "cover" never partially matches inside "cover2" — the closing
        // "}}" already bounds the token, but ordering keeps replacement fully deterministic.
        foreach (var img in images.OrderByDescending(i => i.Name.Length))
        {
            var token = "{{img:" + img.Name + "}}";
            if (!html.Contains(token)) continue;
            var dataUri = $"data:{img.ContentType};base64,{Convert.ToBase64String(img.Data)}";
            html = html.Replace(token, dataUri);
        }

        var trimmed = html.TrimStart();
        if (trimmed.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            return html;

        var title = System.Net.WebUtility.HtmlEncode(a.Title);
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{title}}</title>
            <style>
              :root { color-scheme: light dark; }
              html, body { margin: 0; }
              body {
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                line-height: 1.65; padding: 32px 24px; max-width: 820px; margin: 0 auto;
                color: #1a1a1a; background: #ffffff; -webkit-text-size-adjust: 100%;
              }
              img, video, iframe, table { max-width: 100%; }
              img { height: auto; }
              pre { overflow-x: auto; }
              @media (prefers-color-scheme: dark) { body { color: #e6e6e6; background: #141414; } }
            </style>
            </head>
            <body>
            {{html}}
            </body>
            </html>
            """;
    }

    // Armenia (Asia/Yerevan) is UTC+4 year-round. Resolve via tzdb when present, else a
    // fixed offset (the alpine runtime ships without tzdata + InvariantGlobalization is on).
    private static readonly TimeZoneInfo YerevanTz = ResolveYerevanTz();
    private static TimeZoneInfo ResolveYerevanTz()
    {
        foreach (var id in new[] { "Asia/Yerevan", "Caucasus Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.CreateCustomTimeZone("Yerevan+4", TimeSpan.FromHours(4), "Yerevan", "Yerevan");
    }
    public static DateOnly YerevanToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, YerevanTz));
}
