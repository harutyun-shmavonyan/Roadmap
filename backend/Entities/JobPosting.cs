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

    // --- Optional tailored CV for this specific posting ---
    /// <summary>An ATS-ready PDF résumé tailored to this posting (bytea). Null if none was supplied.</summary>
    public byte[]? TailoredCvPdf { get; set; }
    /// <summary>Human-readable list of what the tailored CV changed vs. the master CV.</summary>
    public string? CvChangeList { get; set; }

    /// <summary>
    /// How well the tailored CV fits this posting's job description, 0–100 (100 = perfect).
    /// Null if not assessed.
    /// </summary>
    public int? CvFitScore { get; set; }
    /// <summary>
    /// JSON array of the gaps keeping the fit below 100, each { "label", "points", "note" },
    /// authored highest-impact-first. `points` is how much closing that gap adds toward 100.
    /// Stored as raw JSON text; the API parses it into a typed list. Null/empty when no gaps.
    /// </summary>
    public string? CvFitGaps { get; set; }

    // --- Application outcome (set by the user from the Jobs tab) ---
    //
    // Without this the pipeline is blind to its own results: a run that produces
    // 27 postings and a run that produces 27 postings nobody hears back from
    // look identical on disk, so a supply problem, a staleness problem and a CV
    // problem are indistinguishable. These fields are what makes "which sources
    // actually convert" answerable.

    /// <summary>
    /// Where this application stands: "none" (default, not applied),
    /// "applied", "screening", "interviewing", "offer", "rejected",
    /// or "ghosted" (applied, no reply, written off). Free text is accepted so
    /// the vocabulary can grow without a migration; the UI offers the set above.
    /// </summary>
    public string? ApplicationStatus { get; set; }

    /// <summary>When the application was sent. Null while status is "none".</summary>
    public DateOnly? AppliedAt { get; set; }

    /// <summary>
    /// When the employer first replied with anything other than an auto-ack.
    /// The gap between this and AppliedAt is the response-time signal; a null
    /// here with an old AppliedAt is what "ghosted" means in practice.
    /// </summary>
    public DateOnly? RespondedAt { get; set; }

    /// <summary>Free-text notes — recruiter name, interview stage, why it died.</summary>
    public string? ApplicationNotes { get; set; }

    /// <summary>Presentation order within the run; the UI pages through postings in this order.</summary>
    public int SortOrder { get; set; }
}
