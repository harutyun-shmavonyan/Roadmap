namespace Roadmap.Api.Entities;

/// <summary>What kind of lexical item this is — it changes how it should be drilled.</summary>
public enum VocabKind
{
    Word,
    Idiom,
    PhrasalVerb,
    Collocation,
}

/// <summary>
/// Roughly how often a native speaker meets this in the wild. Drives whether it is
/// worth actively producing or merely recognising.
/// </summary>
public enum VocabFrequency
{
    VeryCommon,
    Common,
    Uncommon,
    Rare,
}

/// <summary>Where it is socially safe to use the item.</summary>
public enum VocabRegister
{
    Formal,
    Neutral,
    Informal,
    Slang,
    Vulgar,
}

/// <summary>
/// One English item being learned — a word, idiom, phrasal verb or collocation —
/// together with its SM-2 spaced-repetition state.
///
/// Entries are global (not scoped to a roadmap), like <see cref="Note"/>. They are
/// created only on an explicit "save this" and then scheduled by SM-2: every review
/// writes a <see cref="VocabReview"/> row and recomputes <see cref="DueOn"/>, so the
/// review history is a permanent, append-only record of how well the item was digested.
/// </summary>
public class VocabEntry
{
    public Guid Id { get; set; }

    /// <summary>The item itself, e.g. "bite the bullet" or "to hedge".</summary>
    public string Term { get; set; } = string.Empty;

    /// <summary>Normalised form of <see cref="Term"/> (trimmed, lower-cased) used for the uniqueness check.</summary>
    public string NormalizedTerm { get; set; } = string.Empty;

    public VocabKind Kind { get; set; } = VocabKind.Word;

    /// <summary>The English definition.</summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>Armenian gloss, to anchor the meaning. Optional.</summary>
    public string? GlossHy { get; set; }

    /// <summary>Russian gloss, to anchor the meaning. Optional.</summary>
    public string? GlossRu { get; set; }

    public VocabFrequency Frequency { get; set; } = VocabFrequency.Common;
    public VocabRegister Register { get; set; } = VocabRegister.Neutral;

    /// <summary>Natural example sentences showing the item in use.</summary>
    public List<string> Examples { get; set; } = [];

    /// <summary>Words it habitually pairs with (e.g. "bitterly" + "disappointed").</summary>
    public List<string> Collocations { get; set; } = [];

    public List<string> Synonyms { get; set; } = [];

    /// <summary>The mnemonic / etymology / story used to remember it. Kept stable so the hook never changes between reviews.</summary>
    public string? MemoryHook { get; set; }

    /// <summary>Where it was encountered, or why it was asked about.</summary>
    public string? SourceContext { get; set; }

    public string? Notes { get; set; }

    // --- SM-2 scheduling state -------------------------------------------------

    /// <summary>Consecutive successful reviews (grade >= 3). Reset to 0 on a lapse.</summary>
    public int Repetitions { get; set; }

    /// <summary>SM-2 ease factor. Starts at 2.5 and is floored at 1.3.</summary>
    public double EaseFactor { get; set; } = 2.5;

    /// <summary>Current inter-review interval in days. 0 until the first successful review.</summary>
    public int IntervalDays { get; set; }

    /// <summary>The day this entry next comes up for review. A new entry is due immediately.</summary>
    public DateOnly DueOn { get; set; }

    public DateTime? LastReviewedAt { get; set; }

    /// <summary>How many times a previously-learned item was forgotten (graded &lt; 3).</summary>
    public int Lapses { get; set; }

    public int TotalReviews { get; set; }

    /// <summary>The day this item was asked about and saved.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<VocabReview> Reviews { get; set; } = [];
}

/// <summary>
/// One review of one <see cref="VocabEntry"/> — append-only. Records what was asked,
/// what was answered, the grade awarded, and the scheduler state on both sides of the
/// review, so the whole learning curve for an item can be reconstructed.
/// </summary>
public class VocabReview
{
    public Guid Id { get; set; }

    public Guid VocabEntryId { get; set; }
    public VocabEntry? VocabEntry { get; set; }

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    /// <summary>SM-2 quality, 0–5. Below 3 is a failure and resets the interval.</summary>
    public int Grade { get; set; }

    /// <summary>How the item was tested (free-form — the training skill decides). e.g. "produce", "recall", "cloze".</summary>
    public string? PromptType { get; set; }

    /// <summary>What the learner actually answered.</summary>
    public string? Answer { get; set; }

    /// <summary>Why that grade was awarded — the grader's justification.</summary>
    public string? Note { get; set; }

    public int IntervalBefore { get; set; }
    public int IntervalAfter { get; set; }
    public double EaseBefore { get; set; }
    public double EaseAfter { get; set; }
}
