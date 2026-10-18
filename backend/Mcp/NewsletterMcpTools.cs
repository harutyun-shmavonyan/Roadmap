using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;

namespace Roadmap.Api.Mcp;

/// <summary>
/// The Professional Newsletter's agent-facing side: ask where to resume, publish the edition,
/// see what's already there. A run is three calls — <c>get_newsletter_cursor</c>, then the
/// agent's own scanning and rendering, then <c>publish_newsletter</c> — or, for an edition too
/// large for one tool call, <c>start_newsletter_upload</c>, <c>append_newsletter_part</c> per part
/// and <c>finish_newsletter_upload</c>.
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
        "and the moment you finished); omit them and today's cursor window is assumed.\n\n" +
        "One tool call cannot carry much more than ~135 KB: for a larger edition use start_newsletter_upload, " +
        "append_newsletter_part and finish_newsletter_upload instead.")]
    public async Task<string> PublishNewsletter(
        [Description("The complete edition as one self-contained HTML document.")] string html,
        [Description("Edition date (YYYY-MM-DD, Asia/Yerevan). Defaults to today. Republishing a date replaces it.")] string? issue_date = null,
        [Description("Headline for the list row, e.g. \"AI Engineering Daily — 15.09.2026\".")] string? title = null,
        [Description("First day of news covered (YYYY-MM-DD). Defaults to the cursor's window start.")] string? covered_from = null,
        [Description("UTC ISO timestamp the scan reached, normally the moment the run finished. Defaults to now.")] string? covered_until = null,
        [Description("How many stories the edition carries, shown in the list.")] int? item_count = null)
    {
        if (string.IsNullOrWhiteSpace(html)) return J(new { error = "html is required — the edition cannot be empty." });
        var (error, issueDate, coveredFrom, coveredUntil) = ParseMeta(issue_date, covered_from, covered_until);
        if (error is not null) return J(new { error });

        var (issue, replaced, pruned) = await NewsletterLogic.PublishAsync(
            db, html, issueDate, title, coveredFrom, coveredUntil, item_count);
        return Published(issue, replaced, pruned);
    }

    // ── an edition in parts ──

    [McpServerTool(Name = "start_newsletter_upload"), Description(
        "Start publishing an edition in parts — use this instead of publish_newsletter when the HTML is larger than about " +
        "100 KB, since one tool call cannot carry much more. Takes the same metadata as publish_newsletter and returns an " +
        "`upload_id`. Then send the document in order with append_newsletter_part (part 1, 2, 3 …, about 80 KB each — " +
        "split anywhere, the parts are joined exactly as sent) and finish with finish_newsletter_upload, which publishes " +
        "it with the same rules as publish_newsletter. Nothing is published until the finish, so an interrupted upload " +
        "leaves the day's edition as it was; unfinished uploads are cleared after 24 hours.")]
    public async Task<string> StartNewsletterUpload(
        [Description("Edition date (YYYY-MM-DD, Asia/Yerevan). Defaults to today. Publishing a date replaces it.")] string? issue_date = null,
        [Description("Headline for the list row, e.g. \"AI Engineering Daily — 15.09.2026\".")] string? title = null,
        [Description("First day of news covered (YYYY-MM-DD). Defaults to the cursor's window start.")] string? covered_from = null,
        [Description("UTC ISO timestamp the scan reached, normally the moment the run finished. Defaults to the finish time.")] string? covered_until = null,
        [Description("How many stories the edition carries, shown in the list.")] int? item_count = null)
    {
        var (error, issueDate, coveredFrom, coveredUntil) = ParseMeta(issue_date, covered_from, covered_until);
        if (error is not null) return J(new { error });
        var upload = await NewsletterLogic.StartUploadAsync(db, issueDate, title, coveredFrom, coveredUntil, item_count);
        return J(new
        {
            upload_id = upload.Id,
            issue_date = upload.IssueDate.ToString("yyyy-MM-dd"),
            next = "Send the HTML in order with append_newsletter_part (part 1, 2, …), then call finish_newsletter_upload.",
        });
    }

    [McpServerTool(Name = "append_newsletter_part"), Description(
        "Send one part of an edition started with start_newsletter_upload. Parts are numbered from 1 and joined in that " +
        "order exactly as sent, so split the document anywhere (about 80 KB a part). Sending a part number again replaces " +
        "that part, so a retry after an error is safe. Returns the part numbers received so far.")]
    public async Task<string> AppendNewsletterPart(
        [Description("The upload_id from start_newsletter_upload.")] Guid upload_id,
        [Description("This part's number, from 1.")] int part,
        [Description("This part of the HTML document, verbatim.")] string html)
    {
        var (error, received, chars, replaced) = await NewsletterLogic.AddUploadPartAsync(db, upload_id, part, html);
        if (error is not null) return J(new { error });
        return J(new { received_parts = received, total_chars = chars, replaced_part = replaced });
    }

    [McpServerTool(Name = "finish_newsletter_upload"), Description(
        "Publish an edition uploaded with append_newsletter_part: joins the parts in order and publishes them exactly as " +
        "publish_newsletter would (replaces that date's edition, marks it unread, prunes editions older than 14 days). " +
        "Pass `parts`, the number of parts you sent, so a missing one is caught: if any part is missing nothing is " +
        "published, the upload is kept, and the error names the gap — send it and finish again.")]
    public async Task<string> FinishNewsletterUpload(
        [Description("The upload_id from start_newsletter_upload.")] Guid upload_id,
        [Description("How many parts you sent. Recommended: without it the highest part received is taken as the last.")] int? parts = null)
    {
        var (error, issue, replaced, pruned, count, chars) = await NewsletterLogic.FinishUploadAsync(db, upload_id, parts);
        if (error is not null) return J(new { error });
        return Published(issue!, replaced, pruned, new { parts = count, chars });
    }

    private static (string? Error, DateOnly IssueDate, DateOnly? CoveredFrom, DateTime? CoveredUntil) ParseMeta(
        string? issueDate, string? coveredFrom, string? coveredUntil)
    {
        var day = ArticleLogic.YerevanToday();
        if (!string.IsNullOrWhiteSpace(issueDate) && !DateOnly.TryParse(issueDate, out day))
            return ("issue_date must be YYYY-MM-DD.", default, null, null);

        DateOnly? from = null;
        if (!string.IsNullOrWhiteSpace(coveredFrom))
        {
            if (!DateOnly.TryParse(coveredFrom, out var cf)) return ("covered_from must be YYYY-MM-DD.", default, null, null);
            from = cf;
        }

        DateTime? until = null;
        if (!string.IsNullOrWhiteSpace(coveredUntil))
        {
            if (!DateTime.TryParse(coveredUntil, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var cu))
                return ("covered_until must be an ISO timestamp.", default, null, null);
            until = cu;
        }
        return (null, day, from, until);
    }

    private static string Published(Entities.NewsletterIssue issue, bool replaced, int pruned, object? upload = null) => J(new
    {
        issue = NewsletterLogic.ToSummary(issue),
        replaced,
        pruned,
        upload,
        note = replaced
            ? "Replaced the edition already published for this date; it is unread again."
            : "Published a new edition.",
    });

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
