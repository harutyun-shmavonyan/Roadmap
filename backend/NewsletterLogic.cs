using Microsoft.EntityFrameworkCore;
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

    // ── uploading an edition in parts ──

    /// <summary>An upload never finished is cleared this long after it started.</summary>
    public const int UploadExpiryHours = 24;

    /// <summary>Sanity limits on one upload: how many parts, and how large the joined edition.</summary>
    public const int MaxUploadParts = 50;
    public const int MaxEditionChars = 20_000_000;

    /// <summary>
    /// Open an upload for an edition whose metadata has already been checked. Uploads abandoned
    /// longer than <see cref="UploadExpiryHours"/> ago are cleared here, so a run that died midway
    /// leaves nothing behind for long.
    /// </summary>
    public static async Task<NewsletterUpload> StartUploadAsync(Data.RoadmapDbContext db, DateOnly issueDate,
        string? title, DateOnly? coveredFrom, DateTime? coveredUntil, int? itemCount)
    {
        var expired = DateTime.UtcNow.AddHours(-UploadExpiryHours);
        db.NewsletterUploads.RemoveRange(await db.NewsletterUploads.Include(u => u.Parts)
            .Where(u => u.CreatedAt < expired).ToListAsync());
        var upload = new NewsletterUpload
        {
            Id = Guid.NewGuid(), IssueDate = issueDate, Title = title, CoveredFrom = coveredFrom,
            CoveredUntil = coveredUntil is DateTime cu ? NormUtc(cu) : null, ItemCount = itemCount,
        };
        db.NewsletterUploads.Add(upload);
        await db.SaveChangesAsync();
        return upload;
    }

    /// <summary>
    /// Store part <paramref name="partNumber"/> (from 1) of an upload. Sending a number again
    /// replaces that part, so a retried call cannot duplicate content. Returns the part numbers
    /// received so far and the edition's size so far, or an error.
    /// </summary>
    public static async Task<(string? Error, List<int> Received, long TotalChars, bool Replaced)> AddUploadPartAsync(
        Data.RoadmapDbContext db, Guid uploadId, int partNumber, string html)
    {
        if (partNumber < 1 || partNumber > MaxUploadParts)
            return ($"part must be between 1 and {MaxUploadParts}.", [], 0, false);
        if (string.IsNullOrEmpty(html)) return ("a part cannot be empty.", [], 0, false);
        var upload = await db.NewsletterUploads.FirstOrDefaultAsync(u => u.Id == uploadId);
        if (upload is null) return (NoUpload, [], 0, false);

        var others = await db.NewsletterUploadParts.Where(p => p.UploadId == uploadId && p.PartNumber != partNumber)
            .Select(p => new { p.PartNumber, Chars = (long)p.Html.Length }).ToListAsync();
        var total = others.Sum(o => o.Chars) + html.Length;
        if (total > MaxEditionChars) return ($"the edition would exceed {MaxEditionChars:N0} characters.", [], 0, false);

        var part = await db.NewsletterUploadParts.FirstOrDefaultAsync(p => p.UploadId == uploadId && p.PartNumber == partNumber);
        var replaced = part is not null;
        if (part is null) db.NewsletterUploadParts.Add(new NewsletterUploadPart { Id = Guid.NewGuid(), UploadId = uploadId, PartNumber = partNumber, Html = html });
        else part.Html = html;
        upload.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (null, others.Select(o => o.PartNumber).Append(partNumber).Order().ToList(), total, replaced);
    }

    /// <summary>
    /// Join an upload's parts in order and publish them as one edition through
    /// <see cref="PublishAsync"/> — same replace-the-date and prune rules as a single-call publish.
    /// Refuses while any part from 1 to <paramref name="expectedParts"/> (or to the highest part
    /// received) is missing, keeping the upload so the gap can be filled; the upload is removed in
    /// the same save that publishes, so a finished upload cannot be published twice.
    /// </summary>
    public static async Task<(string? Error, NewsletterIssue? Issue, bool Replaced, int Pruned, int Parts, long Chars)> FinishUploadAsync(
        Data.RoadmapDbContext db, Guid uploadId, int? expectedParts)
    {
        var upload = await db.NewsletterUploads.Include(u => u.Parts).FirstOrDefaultAsync(u => u.Id == uploadId);
        if (upload is null) return (NoUpload, null, false, 0, 0, 0);
        var parts = upload.Parts.OrderBy(p => p.PartNumber).ToList();
        if (parts.Count == 0) return ("no parts received yet.", null, false, 0, 0, 0);

        var last = expectedParts ?? parts[^1].PartNumber;
        var have = parts.Select(p => p.PartNumber).ToHashSet();
        var missing = Enumerable.Range(1, Math.Max(0, last)).Where(n => !have.Contains(n)).ToList();
        if (missing.Count > 0)
            return ($"missing part(s) {string.Join(", ", missing)}; send them and finish again.", null, false, 0, 0, 0);
        var extra = parts.Where(p => p.PartNumber > last).Select(p => p.PartNumber).ToList();
        if (extra.Count > 0)
            return ($"received part(s) {string.Join(", ", extra)} beyond the {last} expected.", null, false, 0, 0, 0);

        var html = string.Concat(parts.Select(p => p.Html));
        db.NewsletterUploads.Remove(upload);
        var (issue, replaced, pruned) = await PublishAsync(db, html, upload.IssueDate, upload.Title,
            upload.CoveredFrom, upload.CoveredUntil, upload.ItemCount);
        return (null, issue, replaced, pruned, parts.Count, html.Length);
    }

    private const string NoUpload = "no such upload — it was finished already, expired (uploads are cleared 24 hours after they start), or never started.";

    /// <summary>Postgres stores these as timestamptz, which Npgsql only accepts as UTC.</summary>
    public static DateTime NormUtc(DateTime t) =>
        DateTime.SpecifyKind(t.Kind == DateTimeKind.Utc ? t : t.ToUniversalTime(), DateTimeKind.Utc);

    public static NewsletterSummaryDto ToSummary(NewsletterIssue n) => new(
        n.Id, n.IssueDate.ToString("yyyy-MM-dd"), n.Title,
        n.CoveredFrom.ToString("yyyy-MM-dd"), n.CoveredUntil, n.ItemCount,
        n.IsRead, n.ReadOn?.ToString("yyyy-MM-dd"), n.CreatedAt, n.UpdatedAt);
}
