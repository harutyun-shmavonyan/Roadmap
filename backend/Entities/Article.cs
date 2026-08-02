namespace Roadmap.Api.Entities;

/// <summary>
/// A readable article. Lives in a global library (not tied to a roadmap). The body is either
/// Markdown or a self-contained HTML document (with its own CSS) — see <see cref="Format"/>.
/// HTML articles may reference uploaded <see cref="Images"/> via <c>{{img:NAME}}</c> placeholders
/// that are inlined as data URIs when the standalone document is built. Each article carries an
/// approximate reading time; marking it read credits a custom achievement worth 3 points per
/// hour of reading (see the /read endpoint / MCP tool).
/// </summary>
public class Article
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;   // Markdown body, or raw HTML when Format == "html"

    /// <summary>Body format: "markdown" (default, legacy) or "html" (a full/partial HTML document).</summary>
    public string Format { get; set; } = "markdown";

    /// <summary>Images uploaded for this article, referenced from HTML bodies as {{img:NAME}}.</summary>
    public List<ArticleImage> Images { get; set; } = new();

    /// <summary>
    /// Optional URL of the chat/conversation this article came from (e.g. the ChatGPT thread that
    /// generated it). Surfaced in the reader as a "Chat about this" button. Null when not set.
    /// </summary>
    public string? ChatUrl { get; set; }

    /// <summary>Approximate reading time in minutes. Every article must have one.</summary>
    public int ReadMinutes { get; set; }

    public bool IsRead { get; set; }

    /// <summary>Date the article was marked read (Asia/Yerevan), null while pending.</summary>
    public DateOnly? ReadOn { get; set; }

    /// <summary>
    /// The CustomLog created when this article was marked read, so un-marking can remove the
    /// exact same achievement. Soft reference (no FK) — cleared when the article goes pending.
    /// </summary>
    public Guid? ReadLogId { get; set; }

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
