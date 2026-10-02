namespace Roadmap.Api.Entities;

/// <summary>
/// One thing the screener proposed on one day: a sector panic (A), an index-panic tranche (B) or the
/// monthly cyclical-upturn list (C). Three layers live on the row, written by three different hands:
///
/// 1. <b>The rule's case</b> — written by the screener: the evidence table (each metric against its
///    threshold), the thesis in words, the horizon, the proposal and what would invalidate it. This is
///    deterministic and arrives with the run.
/// 2. <b>The triage verdict</b> — written later by the triage skill after reading filings and news:
///    contagion or impairment, bystanders and epicenter, cited evidence, calibrated confidence. Arrives
///    through <c>set_signal_triage</c>, keyed by <see cref="EventId"/>, and survives a re-publish.
/// 3. <b>The reader's decision</b> — status and notes, set in the tab. Also survives a re-publish.
///
/// <see cref="EventId"/> is the screener's own id (<c>2026-10-01_A_us_semiconductors_t1</c>) and is the
/// identity that holds across two publishes of the same day.
/// </summary>
public class Signal
{
    public Guid Id { get; set; }

    public Guid SignalRunId { get; set; }
    public SignalRun Run { get; set; } = null!;

    public string EventId { get; set; } = string.Empty;

    /// <summary>US | EU.</summary>
    public string Market { get; set; } = "US";

    /// <summary>A (sector panic) | B (index panic) | C (cyclical upturn).</summary>
    public string Screen { get; set; } = "A";

    /// <summary>The industry group (A), the benchmark (B) or "list" (C).</summary>
    public string Key { get; set; } = string.Empty;

    public int Tier { get; set; } = 1;

    /// <summary>Card title: "[US] Semiconductors — sector panic, tier 1".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One sentence: what happened, in numbers.</summary>
    public string Headline { get; set; } = string.Empty;

    /// <summary>Why the rule reads this as an opportunity, in words. Markdown.</summary>
    public string Thesis { get; set; } = string.Empty;

    /// <summary>JSON array of { label, value, threshold, passes, note }: each metric the rule checked.</summary>
    public string EvidenceJson { get; set; } = "[]";

    /// <summary>How long the idea is meant to play out and what ends it, in words.</summary>
    public string Horizon { get; set; } = string.Empty;

    /// <summary>The longest the rule would hold, in trading days, when it has one.</summary>
    public int? HorizonDays { get; set; }

    /// <summary>The proposal in words: basket, tranches, stops. Sizes are proposals for a human; nothing executes.</summary>
    public string? Proposal { get; set; }

    /// <summary>The proposal's numbers as the screener emitted them (JSON).</summary>
    public string? ProposalJson { get; set; }

    /// <summary>JSON array of candidate rows (ticker, metrics, bystander score…), in the screener's order.</summary>
    public string CandidatesJson { get; set; } = "[]";

    /// <summary>Market context at the time (JSON): benchmark drawdown, fear gauge, credit, breadth.</summary>
    public string? ContextJson { get; set; }

    /// <summary>What would prove the thesis wrong, in words.</summary>
    public string? Invalidation { get; set; }

    /// <summary>Screen C only: "on" | "off" — whether the regime allows new entries.</summary>
    public string? Regime { get; set; }

    public string? NextStep { get; set; }

    public int SortOrder { get; set; }

    // ── triage (the LLM's reading of the public record) ──

    /// <summary>The verdict objects as the triage skill wrote them (JSON), event framing included.</summary>
    public string? TriageJson { get; set; }

    /// <summary>The triage narrative for a human: framing, ranked bystanders, do-not-touch, open questions. Markdown.</summary>
    public string? TriageSummary { get; set; }

    public DateTime? TriagedAt { get; set; }
    public string? TriageModel { get; set; }

    // ── the reader's decision ──

    /// <summary>new | reviewed | acted | dismissed. Free text so the vocabulary can grow; the tab offers these four.</summary>
    public string Status { get; set; } = "new";

    public string? Notes { get; set; }
    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
