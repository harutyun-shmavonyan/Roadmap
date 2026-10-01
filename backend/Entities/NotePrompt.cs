namespace Roadmap.Api.Entities;

/// <summary>Whether a prompt takes part in scheduling.</summary>
public enum NotePromptState
{
    /// <summary>Scheduled normally.</summary>
    Active,
    /// <summary>
    /// Fell too far behind to be worth a slot while the daily cap was full (predicted recall below
    /// the park threshold). Not deleted — it is pulled back in on a day with spare slots.
    /// </summary>
    Parked,
    /// <summary>
    /// Out of rotation. Either a leech (failed too many times — the prompt, not the memory, is
    /// usually the problem, so it is flagged for rewriting) or switched off by the learner.
    /// </summary>
    Suspended,
}

/// <summary>
/// One atomic question/answer pair extracted from a <see cref="Note"/> — the unit that is actually
/// scheduled. A daily note holds several facts, and a single grade for the whole note would let the
/// parts the learner knows drag the parts they do not (minimum information principle); so each note
/// yields a handful of prompts, each with its own FSRS state.
///
/// <b>Writing the note counts as the first exposure.</b> A prompt is created already in the state a
/// Good first rating would give (stability ≈ 4 days, due in 4 days) and <see cref="LastReviewedAt"/>
/// starts at creation. Nothing new ever competes for a slot in the daily cap — the cap only triages
/// reviews, which can tolerate triage. The alternative (asking first exposures first) was simulated
/// and held fewer memories: it starved the reviews of things already learned.
/// </summary>
public class NotePrompt
{
    public Guid Id { get; set; }

    public Guid NoteId { get; set; }
    public Note? Note { get; set; }

    /// <summary>The cue. Specific, no hints, answerable from the note alone.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>The expected answer — what the grader compares against. Never shown before the learner answers.</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>Order within the note.</summary>
    public int SortOrder { get; set; }

    public NotePromptState State { get; set; } = NotePromptState.Active;

    // --- FSRS state (see Fsrs) ---------------------------------------------------------------

    /// <summary>FSRS difficulty, 1..10. How hard this prompt is for this learner.</summary>
    public double Difficulty { get; set; }

    /// <summary>FSRS stability in days: the interval after which predicted recall is 90%.</summary>
    public double Stability { get; set; }

    /// <summary>The next day (Asia/Yerevan) this prompt is due.</summary>
    public DateOnly DueOn { get; set; }

    /// <summary>When it was last reviewed — or created, for a prompt never yet asked (creation is the first exposure).</summary>
    public DateTime LastReviewedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The last review was a lapse: it is asked first tomorrow, ahead of everything else.</summary>
    public bool Relearning { get; set; }

    /// <summary>Times it was graded Again.</summary>
    public int Lapses { get; set; }

    /// <summary>Recorded reviews. Creation is not one.</summary>
    public int Reviews { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<NotePromptReview> ReviewHistory { get; set; } = [];
}

/// <summary>
/// One recorded review of a <see cref="NotePrompt"/> — append-only, with the scheduler state on both
/// sides so the learning curve can be reconstructed and the parameters re-fitted later.
/// </summary>
public class NotePromptReview
{
    public Guid Id { get; set; }

    public Guid NotePromptId { get; set; }
    public NotePrompt? NotePrompt { get; set; }

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The local calendar day (Asia/Yerevan) — what the daily cap counts.</summary>
    public DateOnly ReviewDate { get; set; }

    /// <summary>FSRS rating: 1 Again, 2 Hard, 3 Good, 4 Easy.</summary>
    public int Grade { get; set; }

    /// <summary>Calendar days since the previous review (or creation).</summary>
    public int ElapsedDays { get; set; }

    /// <summary>Predicted recall at the moment of the review.</summary>
    public double Retrievability { get; set; }

    /// <summary>This was the next-day re-test after a lapse. Excluded from the true-retention figure.</summary>
    public bool WasRelearning { get; set; }

    public double StabilityBefore { get; set; }
    public double StabilityAfter { get; set; }
    public double DifficultyBefore { get; set; }
    public double DifficultyAfter { get; set; }

    /// <summary>What the learner answered ("O" when they declared it perfectly known).</summary>
    public string? Answer { get; set; }

    /// <summary>The grader's one-line justification.</summary>
    public string? Note { get; set; }
}
