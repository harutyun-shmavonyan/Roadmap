using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;

namespace Roadmap.Api.Mcp;

/// <summary>
/// The Professional Newsletter's agent-facing side: ask where to resume, publish the edition,
/// see what's already there. A run is three calls — <c>get_newsletter_cursor</c>, then the
/// agent's own scanning and rendering, then <c>publish_newsletter</c>.
/// </summary>
[McpServerToolType]
public sealed class NewsletterMcpTools(RoadmapDbContext db)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    [McpServerTool(Name = "get_newsletter_cursor"), Description(
        "Where the next Professional Newsletter edition should start covering the news from. Call this FIRST, " +
        "before scanning anything, and scan the window it returns.\n\n" +
        "`since` (UTC ISO timestamp) is the start of the window: the moment the last edition the reader actually " +
        "ticked as READ stopped scanning, so editions abut exactly instead of overlapping or skipping. With nothing " +
        "read yet it is the newest edition's end; with an empty tab it is 7 days back. It is never more than 14 days " +
        "back — `cappedToMaxWindow` says when that clamp bit, which means older news is deliberately being skipped. " +
        "`anchor` names which of the three cases produced it.")]
    public async Task<string> GetNewsletterCursor()
    {
        var all = await db.NewsletterIssues.AsNoTracking().ToListAsync();
        return J(NewsletterLogic.BuildCursor(all, DateTime.UtcNow));
    }

    [McpServerTool(Name = "list_newsletters"), Description(
        "List the newsletter editions the tab is holding, newest first, with their covered window and read state. " +
        "Omits the HTML. The tab keeps a rolling 14 days; anything older was pruned when the last edition published.")]
    public async Task<string> ListNewsletters()
    {
        var rows = await db.NewsletterIssues.AsNoTracking().OrderByDescending(n => n.IssueDate).ToListAsync();
        return J(rows.Select(NewsletterLogic.ToSummary));
    }

    [McpServerTool(Name = "publish_newsletter"), Description(
        "Publish one edition of the Professional Newsletter and show it in the app's tab.\n\n" +
        "`html` is the whole edition as ONE self-contained HTML document: no external stylesheets, scripts or images " +
        "(inline everything, or use data: URIs), since it is rendered in a sandboxed frame with no network of its own. " +
        "It should read well at 400px wide and respect prefers-color-scheme.\n\n" +
        "One edition per day: publishing again for the same date REPLACES that day's edition and marks it unread again, " +
        "so re-running is safe and never duplicates. Editions older than 14 days are pruned here — the tool reports how " +
        "many went. Pass `covered_from` and `covered_until` from the window you actually scanned (the cursor's `since` " +
        "and the moment you finished); omit them and today's cursor window is assumed.")]
    public async Task<string> PublishNewsletter(
        [Description("The complete edition as one self-contained HTML document.")] string html,
        [Description("Edition date (YYYY-MM-DD, Asia/Yerevan). Defaults to today. Republishing a date replaces it.")] string? issue_date = null,
        [Description("Headline for the list row, e.g. \"AI Engineering Daily — 15.09.2026\".")] string? title = null,
        [Description("First day of news covered (YYYY-MM-DD). Defaults to the cursor's window start.")] string? covered_from = null,
        [Description("UTC ISO timestamp the scan reached, normally the moment the run finished. Defaults to now.")] string? covered_until = null,
        [Description("How many stories the edition carries, shown in the list.")] int? item_count = null)
    {
        if (string.IsNullOrWhiteSpace(html)) return J(new { error = "html is required — the edition cannot be empty." });

        var issueDate = ArticleLogic.YerevanToday();
        if (!string.IsNullOrWhiteSpace(issue_date) && !DateOnly.TryParse(issue_date, out issueDate))
            return J(new { error = "issue_date must be YYYY-MM-DD." });

        DateOnly? coveredFrom = null;
        if (!string.IsNullOrWhiteSpace(covered_from))
        {
            if (!DateOnly.TryParse(covered_from, out var cf)) return J(new { error = "covered_from must be YYYY-MM-DD." });
            coveredFrom = cf;
        }

        DateTime? coveredUntil = null;
        if (!string.IsNullOrWhiteSpace(covered_until))
        {
            if (!DateTime.TryParse(covered_until, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var cu))
                return J(new { error = "covered_until must be an ISO timestamp." });
            coveredUntil = cu;
        }

        var (issue, replaced, pruned) = await NewsletterLogic.PublishAsync(
            db, html, issueDate, title, coveredFrom, coveredUntil, item_count);

        return J(new
        {
            issue = NewsletterLogic.ToSummary(issue),
            replaced,
            pruned,
            note = replaced
                ? "Replaced the edition already published for this date; it is unread again."
                : "Published a new edition.",
        });
    }

    [McpServerTool(Name = "mark_newsletter_read"), Description(
        "Tick an edition as read. This is what moves the cursor: the next run starts where the newest READ edition " +
        "stopped scanning. Normally the reader does this in the app — use it to correct the cursor, not routinely.")]
    public async Task<string> MarkNewsletterRead(
        [Description("Edition date (YYYY-MM-DD). Omit for the most recent edition.")] string? date = null)
    {
        var issue = await FindAsync(date);
        if (issue is null) return J(new { error = "No such edition." });
        issue.IsRead = true;
        issue.ReadOn = ArticleLogic.YerevanToday();
        issue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(NewsletterLogic.ToSummary(issue));
    }

    [McpServerTool(Name = "mark_newsletter_unread"), Description(
        "Untick an edition, sending the cursor back to the edition read before it — so the next run covers that ground again.")]
    public async Task<string> MarkNewsletterUnread(
        [Description("Edition date (YYYY-MM-DD). Omit for the most recent edition.")] string? date = null)
    {
        var issue = await FindAsync(date);
        if (issue is null) return J(new { error = "No such edition." });
        issue.IsRead = false;
        issue.ReadOn = null;
        issue.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(NewsletterLogic.ToSummary(issue));
    }

    private async Task<Entities.NewsletterIssue?> FindAsync(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
            return await db.NewsletterIssues.OrderByDescending(n => n.IssueDate).FirstOrDefaultAsync();
        return DateOnly.TryParse(date, out var d)
            ? await db.NewsletterIssues.FirstOrDefaultAsync(n => n.IssueDate == d)
            : null;
    }
}
