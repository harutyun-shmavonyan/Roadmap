namespace Roadmap.Api.Entities;

/// <summary>
/// A single job posting that survived the Finder pipeline's filters, plus the
/// deterministic signals the pipeline extracted and (optionally) the score an LLM
/// assigned against the user's profile.
/// </summary>
public class JobPosting
{
    public Guid Id { get; set; }

    public Guid JobRunId { get; set; }
    public JobRun Run { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    /// <summary>Which board it came from: remotive | himalayas | remoteok | hackernews | jsearch.</summary>
    public string Source { get; set; } = string.Empty;

    public string? Location { get; set; }
    public DateOnly? PostedAt { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Which output bucket the pipeline routed this to: "armenia-compatible"
    /// (passed every rule) or "eu-allowed" (EU/EEA hiring target, needs an EOR check).
    /// </summary>
    public string Bucket { get; set; } = string.Empty;

    // --- Deterministic signals from FeatureExtractor ---
    public string? SeniorityClass { get; set; }
    public int AiKeywordHits { get; set; }
    public List<string> GeoHints { get; set; } = [];

    /// <summary>Which of the run's queries surfaced this posting. More = stronger signal.</summary>
    public List<string> Queries { get; set; } = [];

    // --- Optional LLM scoring against the profile ---
    public double? Score { get; set; }
    public string? Reasoning { get; set; }

    /// <summary>Presentation order within the run; the UI pages through postings in this order.</summary>
    public int SortOrder { get; set; }
}
