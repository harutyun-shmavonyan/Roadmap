namespace Roadmap.Api.Entities;

/// <summary>
/// One run of the market screener: a trading day, read in the Stock Signals tab.
///
/// At most one run per date (unique index on <see cref="RunDate"/>). Re-publishing a date replaces that
/// day's signals — the screener re-runs a date to repair it, never to add to it — while the reader's own
/// state on each signal (status, notes) and any triage verdict already written survive the replace,
/// keyed by the signal's <c>EventId</c> (see <c>SignalLogic.PublishAsync</c>).
///
/// Most days carry no signal. Those rows exist on purpose: "nothing fired" is the message, and the tab
/// shows it as such rather than as an empty page. A quiet day older than <c>SignalLogic.QuietRetentionDays</c>
/// is pruned on publish; a day that fired is kept — it is the audit trail of what was proposed and why.
/// </summary>
public class SignalRun
{
    public Guid Id { get; set; }

    /// <summary>The trading day the run evaluated (last US close).</summary>
    public DateOnly RunDate { get; set; }

    /// <summary>OK | DEGRADED (a market skipped) | FAILED, as the screener reported it.</summary>
    public string Status { get; set; } = "OK";

    public bool IsTradingDay { get; set; } = true;

    /// <summary>One line for the list row: "No important signal · SPY −0.4% · VIX 17" or "1 signal: Semiconductors sector panic".</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Per-market context as JSON: benchmark, close, 1-day return, 252-day drawdown, fear gauge, credit spread, breadth.</summary>
    public string? MarketsJson { get; set; }

    /// <summary>Groups within reach of a trigger, as JSON — context on a quiet day, never a signal.</summary>
    public string? WatchJson { get; set; }

    /// <summary>The run's Health lines (stale sources, skipped markets). Empty when all was well.</summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>The screener's own daily report, markdown, for the "full report" expander.</summary>
    public string? ReportMarkdown { get; set; }

    /// <summary>Git commit of the screener that produced the run — which rules were in force.</summary>
    public string? GitSha { get; set; }

    public int SignalCount { get; set; }

    public bool IsRead { get; set; }
    public DateOnly? ReadOn { get; set; }

    /// <summary>When the screener published (UTC). Re-publishing moves it.</summary>
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Signal> Signals { get; set; } = [];
}
