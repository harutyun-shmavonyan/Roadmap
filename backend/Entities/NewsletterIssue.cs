namespace Roadmap.Api.Entities;

/// <summary>
/// One edition of the Professional Newsletter: a self-contained HTML page of news, produced by
/// the newsletter agent and read in the app.
///
/// At most one edition per calendar date (unique index on <see cref="IssueDate"/>) — a second run
/// the same day replaces that day's edition rather than stacking a near-duplicate next to it, so
/// re-running is safe. A replacement carries new content, so it comes back unread: the read tick
/// is what tells the next run where to resume (see <c>NewsletterLogic.BuildCursor</c>), and an
/// edition that grew since you ticked it has not, in the part that matters, been read.
///
/// Editions older than <c>NewsletterLogic.RetentionDays</c> are pruned whenever one is published —
/// this is a running window of what's still worth reading, not an archive.
/// </summary>
public class NewsletterIssue
{
    public Guid Id { get; set; }

    /// <summary>The day this edition was published (Asia/Yerevan). One edition per date.</summary>
    public DateOnly IssueDate { get; set; }

    /// <summary>Headline for the list row, e.g. "AI Engineering Daily — 15.09.2026".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// First day of news this edition covers, inclusive — normally the day of the last edition
    /// the reader ticked as read, so nothing between two reads falls through the gap.
    /// </summary>
    public DateOnly CoveredFrom { get; set; }

    /// <summary>
    /// The moment the run finished (UTC): news is covered up to here, and the next run starts
    /// here once this edition is read. Storing the instant rather than the day is what keeps two
    /// runs on the same day from overlapping or skipping.
    /// </summary>
    public DateTime CoveredUntil { get; set; }

    /// <summary>How many stories the edition carries, shown in the list. 0 when the agent didn't say.</summary>
    public int ItemCount { get; set; }

    /// <summary>The edition itself: one self-contained HTML document, rendered in the reader.</summary>
    public string Html { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    /// <summary>Date the edition was ticked read (Asia/Yerevan), null while pending.</summary>
    public DateOnly? ReadOn { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
