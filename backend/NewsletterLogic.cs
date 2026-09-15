using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// The rules behind the Professional Newsletter: where the next run should start reading the
/// world from, and how far back the tab keeps editions. Both are pure functions of the stored
/// editions so the REST endpoints and the MCP tools can't drift apart.
/// </summary>
public static class NewsletterLogic
{
    /// <summary>Editions older than this many days are pruned when a new one is published.</summary>
    public const int RetentionDays = 14;

    /// <summary>
    /// The longest window a single run will be asked to cover. Fall a month behind and the next
    /// edition still only reaches back two weeks — matching what the tab retains, and keeping one
    /// run from trying to boil an ocean of stale news.
    /// </summary>
    public const int MaxWindowDays = 14;

    /// <summary>Where a run starts when there is nothing stored at all.</summary>
    private const int ColdStartDays = 7;

    /// <summary>
    /// Where the next edition should pick up.
    ///
    /// The anchor is the newest edition ticked <b>read</b>, and the window opens at that
    /// edition's <see cref="NewsletterIssue.CoveredUntil"/> — the instant its own run stopped
    /// scanning — so consecutive editions abut exactly rather than overlapping or leaving a gap.
    /// With nothing read yet the newest edition's end is used instead (it's still news you
    /// already have), and with an empty tab the window is <see cref="ColdStartDays"/> days.
    /// Whatever comes out is clamped to <see cref="MaxWindowDays"/>.
    /// </summary>
    public static NewsletterCursorDto BuildCursor(IReadOnlyList<NewsletterIssue> issues, DateTime nowUtc)
    {
        var lastRead = issues.Where(i => i.IsRead).OrderByDescending(i => i.IssueDate).FirstOrDefault();
        var latest = issues.OrderByDescending(i => i.IssueDate).FirstOrDefault();
        var anchor = lastRead ?? latest;

        var since = anchor?.CoveredUntil ?? nowUtc.AddDays(-ColdStartDays);
        var floor = nowUtc.AddDays(-MaxWindowDays);
        var capped = since < floor;
        if (capped) since = floor;

        return new NewsletterCursorDto(
            LastReadDate: lastRead?.IssueDate.ToString("yyyy-MM-dd"),
            LatestIssueDate: latest?.IssueDate.ToString("yyyy-MM-dd"),
            Since: since,
            SinceDate: DateOnly.FromDateTime(since).ToString("yyyy-MM-dd"),
            WindowDays: Math.Max(1, (int)Math.Ceiling((nowUtc - since).TotalDays)),
            CappedToMaxWindow: capped,
            MaxWindowDays: MaxWindowDays,
            RetentionDays: RetentionDays,
            UnreadCount: issues.Count(i => !i.IsRead),
            IssueCount: issues.Count,
            Anchor: lastRead is not null ? "last-read" : latest is not null ? "latest-unread" : "cold-start");
    }

    /// <summary>The oldest issue date the tab still keeps, given the day a publish happens.</summary>
    public static DateOnly OldestKept(DateOnly today) => today.AddDays(-(RetentionDays - 1));

    /// <summary>
    /// Store an edition, replacing the one already published for that date, and prune whatever
    /// has fallen out of the retention window. Both the REST endpoint and the MCP tool go through
    /// here so a run gets the same answer whichever door it comes in by.
    /// </summary>
    public static async Task<(NewsletterIssue Issue, bool Replaced, int Pruned)> PublishAsync(
        Data.RoadmapDbContext db, string html, DateOnly issueDate, string? title,
        DateOnly? coveredFrom, DateTime? coveredUntil, int? itemCount)
    {
        var all = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(db.NewsletterIssues);
        var cursor = BuildCursor(all, DateTime.UtcNow);

        var issue = all.FirstOrDefault(i => i.IssueDate == issueDate);
        var replaced = issue is not null;
        if (issue is null)
        {
            issue = new NewsletterIssue { Id = Guid.NewGuid(), IssueDate = issueDate };
            db.NewsletterIssues.Add(issue);
        }

        issue.Html = html;
        issue.Title = string.IsNullOrWhiteSpace(title)
            ? $"Professional Newsletter — {issueDate:dd.MM.yyyy}"
            : title.Trim()[..Math.Min(title.Trim().Length, 256)];
        issue.CoveredFrom = coveredFrom ?? DateOnly.FromDateTime(cursor.Since);
        issue.CoveredUntil = NormUtc(coveredUntil ?? DateTime.UtcNow);
        issue.ItemCount = Math.Max(0, itemCount ?? 0);
        // A replacement is new content, so it is unread again — the read tick is what the next
        // run resumes from, and ticking the shorter edition never meant "I read the longer one".
        issue.IsRead = false;
        issue.ReadOn = null;
        issue.UpdatedAt = DateTime.UtcNow;

        var oldest = OldestKept(ArticleLogic.YerevanToday());
        var stale = all.Where(i => i.IssueDate < oldest).ToList();
        db.NewsletterIssues.RemoveRange(stale);

        await db.SaveChangesAsync();
        return (issue, replaced, stale.Count);
    }

    /// <summary>Postgres stores these as timestamptz, which Npgsql only accepts as UTC.</summary>
    public static DateTime NormUtc(DateTime t) =>
        DateTime.SpecifyKind(t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime(), DateTimeKind.Utc);

    public static NewsletterSummaryDto ToSummary(NewsletterIssue n) => new(
        n.Id, n.IssueDate.ToString("yyyy-MM-dd"), n.Title,
        n.CoveredFrom.ToString("yyyy-MM-dd"), n.CoveredUntil, n.ItemCount,
        n.IsRead, n.ReadOn?.ToString("yyyy-MM-dd"), n.CreatedAt, n.UpdatedAt);
}
