namespace Roadmap.Api.Entities;

/// <summary>Whether a prompt takes part in scheduling.</summary>
public enum FlashcardPromptState
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
/// Notes v2. A flashcard is <b>one note</b> — a single top-level bullet with its subpoints — in one
/// book ('red' | 'green'). The card is the entity; the date is just a property of it, used to filter
/// ("all cards of 2026-09-30") and as the first exposure of a backfilled card. A day can hold any
/// number of cards; adding a note always creates a new card, never appends to another one.
///
/// It lives beside the v1 daily notes and shares nothing with them: no foreign key, no shared table,
/// no shared logic. What is actually scheduled is the card's <see cref="FlashcardPrompt"/>s — as many
/// as its subpoints need, one fact each — and each prompt keeps its own review history.
/// </summary>
public class Flashcard
{
    public Guid Id { get; set; }

    /// <summary>'red' (professional/technical) or 'green' (general learning).</summary>
    public string Book { get; set; } = string.Empty;

    /// <summary>The calendar date this note was learned (Asia/Yerevan). A filter, not an identity.</summary>
    public DateOnly EntryDate { get; set; }

    /// <summary>The note: one top-level bullet and its subpoints, Markdown.</summary>
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<FlashcardPrompt> Prompts { get; set; } = [];
}

/// <summary>
/// One atomic question/answer pair on a <see cref="Flashcard"/> — the unit that is scheduled. A
/// single grade for a whole card would let the facts the learner knows drag the ones they do not
/// (minimum information principle), so each subpoint worth recalling gets its own prompt with its
/// own FSRS state.
///
/// <b>Writing the card counts as the first exposure.</b> A prompt is created already in the state a
/// Good first rating would give (stability ≈ 4 days, due in 4 days) and <see cref="LastReviewedAt"/>
/// starts at creation — or, for a backfilled card, at the card's own date. Nothing new ever competes
/// for a slot in the daily cap; the cap only triages reviews.
/// </summary>
public class FlashcardPrompt
{
    public Guid Id { get; set; }

    public Guid FlashcardId { get; set; }
    public Flashcard? Flashcard { get; set; }

    /// <summary>The cue. Specific, no hints, answerable from the card alone.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>The expected answer — what the grader compares against. Never shown before the learner answers.</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>Order within the card.</summary>
    public int SortOrder { get; set; }

    public FlashcardPromptState State { get; set; } = FlashcardPromptState.Active;

    // --- FSRS state (see Fsrs) ---------------------------------------------------------------

    /// <summary>FSRS difficulty, 1..10. How hard this prompt is for this learner.</summary>
    public double Difficulty { get; set; }

    /// <summary>FSRS stability in days: the interval after which predicted recall is 90%.</summary>
    public double Stability { get; set; }

    /// <summary>The next day (Asia/Yerevan) this prompt is due.</summary>
    public DateOnly DueOn { get; set; }

    /// <summary>When it was last reviewed — or first exposed, for a prompt never yet asked.</summary>
    public DateTime LastReviewedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The last review was a lapse: it is asked first tomorrow, ahead of everything else.</summary>
    public bool Relearning { get; set; }

    /// <summary>Times it was graded Again.</summary>
    public int Lapses { get; set; }

    /// <summary>Recorded reviews. Creation is not one.</summary>
    public int Reviews { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<FlashcardReview> ReviewHistory { get; set; } = [];
}

/// <summary>
/// One recorded review of a <see cref="FlashcardPrompt"/> — append-only, with the scheduler state on
/// both sides so the learning curve can be reconstructed and the parameters re-fitted later.
/// </summary>
public class FlashcardReview
{
    public Guid Id { get; set; }

    public Guid FlashcardPromptId { get; set; }
    public FlashcardPrompt? FlashcardPrompt { get; set; }

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The local calendar day (Asia/Yerevan) — what the daily cap counts.</summary>
    public DateOnly ReviewDate { get; set; }

    /// <summary>FSRS rating: 1 Again, 2 Hard, 3 Good, 4 Easy.</summary>
    public int Grade { get; set; }

    /// <summary>Calendar days since the previous review (or the exposure).</summary>
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
