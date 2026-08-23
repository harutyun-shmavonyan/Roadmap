using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

/// <summary>
/// CRUD over the global Markdown article library, plus marking an article read/unread.
/// Reading records a date and nothing else — it earns no points and creates no
/// achievement. Articles read before that changed still carry a log, which the
/// unread and delete paths clean up.
/// </summary>
[McpServerToolType]
public sealed class ArticleMcpTools(RoadmapDbContext db, IHttpClientFactory httpFactory)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    // Load an article's image metadata (no bytes) as DTOs for the detail payload.
    private Task<List<ArticleImageDto>> LoadImageDtos(Guid articleId) =>
        db.ArticleImages.AsNoTracking().Where(i => i.ArticleId == articleId)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
            .Select(i => new ArticleImageDto(i.Name, i.ContentType, i.SortOrder)).ToListAsync();

    private static string NormFormat(string? f) =>
        string.Equals(f?.Trim(), "html", StringComparison.OrdinalIgnoreCase) ? "html" : "markdown";

    [McpServerTool(Name = "list_articles"), Description("List all articles in the reading library (title, body format, reading time, points, read status, image count) — newest/sorted first. Omits the body.")]
    public async Task<string> ListArticles()
    {
        var rows = await db.Articles.AsNoTracking()
            .OrderBy(a => a.SortOrder).ThenByDescending(a => a.CreatedAt)
            .Select(a => new { A = a, C = a.Images.Count }).ToListAsync();
        return J(rows.Select(r => ArticleLogic.ToSummary(r.A, r.C)));
    }

    [McpServerTool(Name = "get_article"), Description("Get one article by its UUID, including its full body (Markdown or HTML), its format, and the list of uploaded images (names only).")]
    public async Task<string> GetArticle([Description("Article UUID")] Guid article_id)
    {
        var a = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == article_id);
        return a is null ? J(new { error = "Article not found" }) : J(ArticleLogic.ToDetail(a, await LoadImageDtos(article_id)));
    }

    [McpServerTool(Name = "create_article"), Description(
        "Create an article. content_format is 'markdown' (default) or 'html'. For 'html', pass a self-contained HTML body — you may include a full document (<!doctype html>…) with its own <style>, or just a fragment which gets wrapped in a responsive shell. Reference uploaded images with the placeholder {{img:NAME}} in an <img> src (or CSS url()); upload the bytes afterward with add_article_image using the same NAME. read_minutes is the approximate reading time; if omitted it is estimated from word count (~200 wpm, HTML tags stripped). The article is rendered in the Roadmap app and can be opened full-screen in a new browser tab.")]
    public async Task<string> CreateArticle(
        [Description("Article title")] string title,
        [Description("Body content — Markdown, or HTML when content_format is 'html'")] string content,
        [Description("Body format: 'markdown' (default) or 'html'")] string? content_format = null,
        [Description("Approximate reading time in minutes (optional — auto-estimated when omitted)")] int? read_minutes = null,
        [Description("Optional URL of the chat/conversation this article came from (e.g. the ChatGPT thread). Shown in the reader as a 'Chat about this' button.")] string? chat_url = null)
    {
        if (string.IsNullOrWhiteSpace(title)) return J(new { error = "Title is required." });
        var format = NormFormat(content_format);
        var body = content ?? "";
        var maxSort = await db.Articles.MaxAsync(a => (int?)a.SortOrder) ?? -1;
        var a = new Article
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Content = body,
            Format = format,
            ChatUrl = ArticleLogic.NormChatUrl(chat_url),
            ReadMinutes = read_minutes is > 0 ? read_minutes.Value : ArticleLogic.EstimateReadMinutes(body, format),
            SortOrder = maxSort + 1,
        };
        db.Articles.Add(a);
        await db.SaveChangesAsync();
        return J(ArticleLogic.ToDetail(a));
    }

    [McpServerTool(Name = "update_article"), Description(
        "Update an article's title, body, format, and/or reading time. Only the fields you pass change. Set content_format to 'html' to switch the body to a self-contained HTML document (reference uploaded images as {{img:NAME}}). When content changes without an explicit read_minutes, the reading time is re-estimated.")]
    public async Task<string> UpdateArticle(
        [Description("Article UUID")] Guid article_id,
        [Description("New title (optional)")] string? title = null,
        [Description("New body content — Markdown or HTML (optional)")] string? content = null,
        [Description("New body format: 'markdown' or 'html' (optional)")] string? content_format = null,
        [Description("New reading time in minutes (optional)")] int? read_minutes = null,
        [Description("Chat/conversation URL to link from the reader (optional; pass an empty string to clear it)")] string? chat_url = null)
    {
        var a = await db.Articles.FirstOrDefaultAsync(x => x.Id == article_id);
        if (a is null) return J(new { error = "Article not found" });
        if (!string.IsNullOrWhiteSpace(title)) a.Title = title!.Trim();
        if (content != null) a.Content = content;
        if (content_format != null) a.Format = NormFormat(content_format);
        if (chat_url != null) a.ChatUrl = ArticleLogic.NormChatUrl(chat_url);
        if (read_minutes is > 0) a.ReadMinutes = read_minutes.Value;
        else if (content != null) a.ReadMinutes = ArticleLogic.EstimateReadMinutes(a.Content, a.Format);
        a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(ArticleLogic.ToDetail(a, await LoadImageDtos(article_id)));
    }

    [McpServerTool(Name = "delete_article"), Description("Delete an article. If it was read back when reading still earned points, that old achievement is removed too.")]
    public async Task<string> DeleteArticle([Description("Article UUID")] Guid article_id)
    {
        var a = await db.Articles.FirstOrDefaultAsync(x => x.Id == article_id);
        if (a is null) return J(new { error = "Article not found" });
        if (a.ReadLogId is Guid logId)
        {
            var log = await db.CustomLogs.FirstOrDefaultAsync(c => c.Id == logId);
            if (log != null) db.CustomLogs.Remove(log);
        }
        db.Articles.Remove(a);
        await db.SaveChangesAsync();
        return J(new { deleted = true, article_id });
    }

    [McpServerTool(Name = "mark_article_read"), Description("Mark an article read on the given date (defaults to today, Asia/Yerevan). This records the date only — reading does not earn points and creates no achievement. Idempotent.")]
    public async Task<string> MarkArticleRead(
        [Description("Article UUID")] Guid article_id,
        [Description("Date the article was read (YYYY-MM-DD, optional — defaults to today)")] string? date = null)
    {
        var a = await db.Articles.FirstOrDefaultAsync(x => x.Id == article_id);
        if (a is null) return J(new { error = "Article not found" });
        if (a.IsRead) return J(ArticleLogic.ToDetail(a));

        DateOnly pd = ArticleLogic.YerevanToday();
        if (!string.IsNullOrWhiteSpace(date) && !DateOnly.TryParse(date, out pd))
            return J(new { error = "Invalid date. Use YYYY-MM-DD." });

        a.IsRead = true;
        a.ReadOn = pd;
        a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(ArticleLogic.ToDetail(a));
    }

    [McpServerTool(Name = "mark_article_unread"), Description("Mark an article pending again. If it carries an achievement from back when reading earned points, that is removed too.")]
    public async Task<string> MarkArticleUnread([Description("Article UUID")] Guid article_id)
    {
        var a = await db.Articles.FirstOrDefaultAsync(x => x.Id == article_id);
        if (a is null) return J(new { error = "Article not found" });
        if (a.ReadLogId is Guid logId)
        {
            var log = await db.CustomLogs.FirstOrDefaultAsync(c => c.Id == logId);
            if (log != null) db.CustomLogs.Remove(log);
        }
        a.IsRead = false;
        a.ReadOn = null;
        a.ReadLogId = null;
        a.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(ArticleLogic.ToDetail(a));
    }

    [McpServerTool(Name = "add_article_image"), Description(
        "Upload (or replace) one image on an article, for use in an HTML body. Pass the raw bytes as base64 in data_base64 (a full 'data:...;base64,...' URI is also accepted). The image is stored under NAME (its filename); reference it in the HTML as {{img:NAME}}, which is inlined as a data URI when the article renders. Re-uploading the same NAME replaces it. Call once per image to keep payloads small.")]
    public async Task<string> AddArticleImage(
        [Description("Article UUID")] Guid article_id,
        [Description("Reference name / filename, e.g. 'cover.png' — used as {{img:cover.png}}")] string name,
        [Description("Image bytes as base64 (or a full data: URI)")] string data_base64,
        [Description("MIME type, e.g. 'image/png' (optional — guessed from the name when omitted)")] string? content_type = null)
    {
        var article = await db.Articles.FirstOrDefaultAsync(x => x.Id == article_id);
        if (article is null) return J(new { error = "Article not found" });

        var cleanName = ArticleLogic.SanitizeImageName(name);
        if (string.IsNullOrEmpty(cleanName)) return J(new { error = "A usable image name is required." });

        // Accept a bare base64 string or a full data: URI (strip the 'data:...;base64,' prefix).
        var b64 = data_base64?.Trim() ?? "";
        var mime = content_type;
        if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = b64.IndexOf(',');
            if (comma < 0) return J(new { error = "Malformed data URI." });
            var header = b64[5..comma]; // e.g. "image/png;base64"
            if (string.IsNullOrWhiteSpace(mime) && header.Length > 0)
                mime = header.Split(';')[0];
            b64 = b64[(comma + 1)..];
        }
        byte[] bytes;
        try { bytes = Convert.FromBase64String(b64); }
        catch (FormatException) { return J(new { error = "data_base64 is not valid base64." }); }
        if (bytes.Length == 0) return J(new { error = "Image data is empty." });
        // Note: even under this cap, base64 through the MCP tool call is limited by the model's
        // context — for images more than a few MB, prefer the multipart upload endpoint.
        if (bytes.Length > 40 * 1024 * 1024) return J(new { error = "Image exceeds 40 MB." });

        var ct = string.IsNullOrWhiteSpace(mime) ? ArticleLogic.GuessContentType(cleanName) : mime!.Trim();
        var existing = await db.ArticleImages.FirstOrDefaultAsync(i => i.ArticleId == article_id && i.Name == cleanName);
        if (existing is null)
        {
            var maxSort = await db.ArticleImages.Where(i => i.ArticleId == article_id).MaxAsync(i => (int?)i.SortOrder) ?? -1;
            db.ArticleImages.Add(new ArticleImage
            {
                Id = Guid.NewGuid(), ArticleId = article_id, Name = cleanName,
                ContentType = ct, Data = bytes, SortOrder = maxSort + 1,
            });
        }
        else { existing.ContentType = ct; existing.Data = bytes; }
        article.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(new { article_id, name = cleanName, content_type = ct, bytes = bytes.Length, reference = $"{{{{img:{cleanName}}}}}" });
    }

    [McpServerTool(Name = "add_article_image_from_url"), Description(
        "Add (or replace) an article image by having the server download it from an http(s) URL — the image bytes never pass through the tool call, so there is no token cost or size limit from base64. Ideal for storing an AI-generated image (e.g. a ChatGPT/DALL·E image at its hosted URL) or any image already on the web. The URL must be publicly reachable by the server (no login/cookies); it is fetched immediately, so temporary signed URLs are fine. Stored under NAME and referenced in an HTML body as {{img:NAME}}. Re-using the same NAME replaces it.")]
    public async Task<string> AddArticleImageFromUrl(
        [Description("Article UUID")] Guid article_id,
        [Description("Reference name / filename, e.g. 'cover.png' — used as {{img:cover.png}}")] string name,
        [Description("Public http(s) URL of the image to download")] string url,
        [Description("MIME type, e.g. 'image/png' (optional — taken from the response, else guessed from the name)")] string? content_type = null)
    {
        var article = await db.Articles.FirstOrDefaultAsync(x => x.Id == article_id);
        if (article is null) return J(new { error = "Article not found" });

        var cleanName = ArticleLogic.SanitizeImageName(name);
        if (string.IsNullOrEmpty(cleanName)) return J(new { error = "A usable image name is required." });

        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return J(new { error = "url must be an absolute http(s) URL." });
        if (await UrlGuard.IsBlockedHostAsync(uri))
            return J(new { error = "url host is not allowed (private/loopback addresses are blocked)." });

        byte[] bytes;
        string? headerType;
        try
        {
            var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RoadmapBot/1.0");
            using var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode) return J(new { error = $"Fetch failed: HTTP {(int)resp.StatusCode}" });
            headerType = resp.Content.Headers.ContentType?.MediaType;

            const long cap = 40L * 1024 * 1024;
            await using var src = await resp.Content.ReadAsStreamAsync();
            using var ms = new MemoryStream();
            var buf = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(buf)) > 0)
            {
                if (ms.Length + read > cap) return J(new { error = "Image exceeds 40 MB." });
                ms.Write(buf, 0, read);
            }
            bytes = ms.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return J(new { error = $"Could not download the image: {ex.Message}" });
        }
        if (bytes.Length == 0) return J(new { error = "Downloaded image is empty." });

        var ct = !string.IsNullOrWhiteSpace(content_type) ? content_type!.Trim()
            : !string.IsNullOrWhiteSpace(headerType) ? headerType!
            : ArticleLogic.GuessContentType(cleanName);

        var existing = await db.ArticleImages.FirstOrDefaultAsync(i => i.ArticleId == article_id && i.Name == cleanName);
        if (existing is null)
        {
            var maxSort = await db.ArticleImages.Where(i => i.ArticleId == article_id).MaxAsync(i => (int?)i.SortOrder) ?? -1;
            db.ArticleImages.Add(new ArticleImage
            {
                Id = Guid.NewGuid(), ArticleId = article_id, Name = cleanName,
                ContentType = ct, Data = bytes, SortOrder = maxSort + 1,
            });
        }
        else { existing.ContentType = ct; existing.Data = bytes; }
        article.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(new { article_id, name = cleanName, content_type = ct, bytes = bytes.Length, source_url = uri.ToString(), reference = $"{{{{img:{cleanName}}}}}" });
    }

    [McpServerTool(Name = "list_article_images"), Description("List the images uploaded to an article (name, MIME type, byte size) and the {{img:NAME}} reference to use in an HTML body.")]
    public async Task<string> ListArticleImages([Description("Article UUID")] Guid article_id)
    {
        if (!await db.Articles.AnyAsync(x => x.Id == article_id)) return J(new { error = "Article not found" });
        var imgs = await db.ArticleImages.AsNoTracking().Where(i => i.ArticleId == article_id)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
            .Select(i => new { i.Name, i.ContentType, bytes = i.Data.Length })
            .ToListAsync();
        return J(imgs.Select(i => new { i.Name, i.ContentType, i.bytes, reference = $"{{{{img:{i.Name}}}}}" }));
    }

    [McpServerTool(Name = "delete_article_image"), Description("Delete one image from an article by its NAME. Any {{img:NAME}} placeholder still in the body will no longer resolve.")]
    public async Task<string> DeleteArticleImage(
        [Description("Article UUID")] Guid article_id,
        [Description("Image reference name (e.g. 'cover.png')")] string name)
    {
        var cleanName = ArticleLogic.SanitizeImageName(name);
        var img = await db.ArticleImages.FirstOrDefaultAsync(i => i.ArticleId == article_id && i.Name == cleanName);
        if (img is null) return J(new { error = "Image not found" });
        db.ArticleImages.Remove(img);
        await db.SaveChangesAsync();
        return J(new { deleted = true, article_id, name = cleanName });
    }
}
