namespace Roadmap.Api.Entities;

/// <summary>
/// A readable article. Lives in a global library (not tied to a roadmap). The body is either
/// Markdown or a self-contained HTML document (with its own CSS) — see <see cref="Format"/>.
/// HTML articles may reference uploaded <see cref="Images"/> via <c>{{img:NAME}}</c> placeholders
/// that are inlined as data URIs when the standalone document is built. Each article carries an
/// approximate reading time; marking it read records the date and nothing more — reading
/// earns no points (see the /read endpoint / MCP tool).
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

    /// <summary>
    /// How far the reader got, as a fraction (0..1) of the article's scrollable length. Stored as a
    /// fraction rather than pixels so the spot survives a different window size, font size or device.
    /// The reader posts it as you scroll and jumps back to it the next time the article is opened —
    /// in the app, in the "open in new tab" view, or on another device.
    /// </summary>
    public double ReadProgress { get; set; }

    /// <summary>
    /// The exact spot, as "<c>index:offset</c>": the index of the block element (paragraph, heading,
    /// list item, image…) that sat at the top of the reader, and how many pixels into it you were.
    /// Unlike <see cref="ReadProgress"/> this survives a reflow — a narrower window, a bigger font,
    /// a phone instead of a laptop — so reopening lands on the same sentence rather than nearby.
    /// Null when the position is too near the top to be worth anchoring, or when the reader could
    /// not resolve one; the fraction is then used instead.
    /// </summary>
    public string? ReadAnchor { get; set; }

    /// <summary>When the reading position was last recorded (UTC); null while never opened.</summary>
    public DateTime? ProgressAt { get; set; }

    /// <summary>Date the article was marked read (Asia/Yerevan), null while pending.</summary>
    public DateOnly? ReadOn { get; set; }

    /// <summary>
    /// Legacy: the CustomLog created back when marking an article read still earned points.
    /// Nothing sets this any more, but rows read before that change still carry one, so the
    /// unread and delete paths keep removing it. Soft reference (no FK).
    /// </summary>
    public Guid? ReadLogId { get; set; }

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
