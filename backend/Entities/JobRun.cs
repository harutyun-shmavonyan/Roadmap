namespace Roadmap.Api.Entities;

/// <summary>
/// One day's job-scouting run, imported from the Finder pipeline.
///
/// At most one run per calendar date (unique index on RunDate) — re-importing the
/// same date replaces that day's postings rather than accumulating duplicates, so
/// running the scout twice in a day is safe and idempotent. The Jobs tab treats a
/// run as "a day", which is why the date is the natural key rather than the id.
/// </summary>
public class JobRun
{
    public Guid Id { get; set; }

    /// <summary>The calendar date this run was scouted for (Asia/Yerevan).</summary>
    public DateOnly RunDate { get; set; }

    /// <summary>The queries the scout ran, e.g. ["senior backend engineer", ...].</summary>
    public List<string> Queries { get; set; } = [];

    /// <summary>Max posting age in days that the run filtered on.</summary>
    public int MaxAgeDays { get; set; }

    /// <summary>Total postings ingested across all sources before filtering.</summary>
    public int RawCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<JobPosting> Postings { get; set; } = [];
}
