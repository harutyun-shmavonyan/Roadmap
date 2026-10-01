using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;

namespace Roadmap.Api.Mcp;

/// <summary>
/// The Stock Signals tab's agent-facing side. Two writers use it: the market screener, which publishes
/// every run (quiet days included) with <c>publish_signal_run</c>, and the triage skill, which reads the
/// public record after a signal fires and writes its verdict back with <c>set_signal_triage</c>.
/// </summary>
[McpServerToolType]
public sealed class SignalMcpTools(RoadmapDbContext db)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    [McpServerTool(Name = "publish_signal_run"), Description(
        "Publish one market-screener run to the Stock Signals tab. Call it after EVERY run, including the quiet " +
        "ones: a day with no signal is stored and shown as 'No important signal' with its market context, which is " +
        "the whole point of the tab. One run per date — publishing a date again REPLACES that day's signals, while " +
        "the reader's status and notes on each signal and any triage verdict already written are carried across by " +
        "event_id. A re-publish with the same set of event_ids keeps the day's read tick; one that changes the set " +
        "makes the day unread.\n\n" +
        "Each signal carries the rule's own case so a reader can judge it without the screener in front of them: " +
        "`headline` (what happened, in numbers), `thesis` (why the rule reads it as an opportunity, markdown), " +
        "`evidence` (JSON array of {label, value, threshold, passes, note}: every metric the rule checked), " +
        "`horizon` (how long it plays out and what ends it) with `horizon_days`, `proposal` (basket, tranches, stops, " +
        "in words — sizes are proposals for a human, nothing executes) with `proposal_detail` (the numbers), " +
        "`candidates` (JSON array of rows), `context`, `invalidation` (what would prove it wrong), `regime` (Screen C).\n\n" +
        "Returns { run, action: 'created' | 'replaced', carried, pruned }. `carried` is how many signals kept a " +
        "status, notes or triage from the previous publish — if it is 0 on a day the reader had acted, say so.")]
    public async Task<string> PublishSignalRun(
        [Description("The trading day the run evaluated (YYYY-MM-DD).")] string run_date,
        [Description("The signals that fired, in the order to show them. Omit or pass [] for a quiet day. Each needs event_id, screen (A|B|C) and key.")]
        SignalInput[]? signals = null,
        [Description("OK | DEGRADED (a market was skipped) | FAILED. Defaults to OK.")] string? status = null,
        [Description("False on a holiday or when no market had prices. Defaults to true.")] bool? is_trading_day = null,
        [Description("One line for the list row, e.g. 'No important signal · SPY −0.4% · VIX 17'. Defaults to a generic line.")] string? summary = null,
        [Description("Per-market context as a JSON object keyed by market: {US: {benchmark, close, ret_1d, dd_252d, vol_name, vol, hy_oas, hy_oas_20d_change, breadth}, EU: {...}}.")]
        JsonElement? markets = null,
        [Description("Groups within reach of a trigger, as a JSON array of {market, group, z, dd_20d, rev, cutting, note}. Shown muted on a quiet day.")]
        JsonElement? watch = null,
        [Description("The run's Health lines (stale estimates, skipped market). Omit when all was well.")] string[]? warnings = null,
        [Description("The screener's daily report as markdown, for the 'full report' expander.")] string? report_markdown = null,
        [Description("Git commit of the screener that produced the run.")] string? git_sha = null)
    {
        var input = new PublishSignalRunInput
        {
            run_date = run_date, signals = signals, status = status, is_trading_day = is_trading_day, summary = summary,
            markets = markets, watch = watch, warnings = warnings, report_markdown = report_markdown, git_sha = git_sha,
        };
        var error = SignalLogic.Validate(input);
        if (error is not null) return J(new { error });
        var (run, action, carried, pruned) = await SignalLogic.PublishAsync(db, input);
        return J(new
        {
            run = SignalLogic.ToSummary(run), action, carried, pruned,
            note = run.SignalCount == 0
                ? "Quiet day stored; the tab shows 'No important signal' with the market context."
                : $"{run.SignalCount} signal(s) stored. Run the triage skill on each and write its verdict back with set_signal_triage.",
        });
    }

    [McpServerTool(Name = "list_signal_runs"), Description(
        "The screener's runs, newest first: date, status, one-line summary, how many signals fired, read state. " +
        "Quiet days are listed too. Use it to see what the tab holds and which days still have unread signals.")]
    public async Task<string> ListSignalRuns([Description("How many runs to return. Defaults to 30.")] int n = 30)
    {
        var rows = await db.SignalRuns.AsNoTracking().OrderByDescending(r => r.RunDate).Take(Math.Max(1, n)).ToListAsync();
        return J(rows.Select(SignalLogic.ToSummary));
    }

    [McpServerTool(Name = "get_signal_run"), Description(
        "One run in full: market context, watch list, health warnings, the report, and every signal with its evidence, " +
        "thesis, horizon, proposal, candidates, triage verdict and the reader's status and notes. Omit the date for the latest run.")]
    public async Task<string> GetSignalRun([Description("Run date (YYYY-MM-DD). Omit for the most recent run.")] string? date = null)
    {
        if (!string.IsNullOrWhiteSpace(date) && !SignalLogic.TryParseDate(date, out _)) return J(new { error = "date must be YYYY-MM-DD." });
        var run = await SignalLogic.FindRunAsync(db, date);
        return run is null ? J(new { error = "No such run." }) : J(SignalLogic.ToDto(run));
    }

    [McpServerTool(Name = "set_signal_triage"), Description(
        "Attach the triage skill's verdict to a signal, by the screener's event_id. `triage` is the verdict JSON exactly " +
        "as written to triage/<event_id>.json (the array of per-candidate objects plus the _EVENT framing); `summary` is " +
        "the human-readable reading (markdown): the event framing, the ranked cleanest bystanders with their single " +
        "strongest piece of evidence, the do-not-touch names, the open questions. It shows under the signal in the tab, " +
        "and the day comes back unread so the reader sees it. Re-calling replaces the previous verdict.")]
    public async Task<string> SetSignalTriage(
        [Description("The signal's event_id, e.g. 2026-10-01_A_us_semiconductors_t1.")] string event_id,
        [Description("The verdict JSON (array of verdict objects, Appendix B schema, including the _EVENT object).")] JsonElement? triage = null,
        [Description("The narrative for a human, markdown. Phone-readable: framing, ranked bystanders, do-not-touch, open questions.")] string? summary = null,
        [Description("The model id that produced the verdict.")] string? model = null)
    {
        if (string.IsNullOrWhiteSpace(event_id)) return J(new { error = "event_id is required." });
        if (triage is null && string.IsNullOrWhiteSpace(summary)) return J(new { error = "Pass triage, summary, or both." });
        var s = await SignalLogic.SetTriageAsync(db, event_id, triage, summary, model);
        return s is null ? J(new { error = $"No signal with event_id '{event_id}'. Publish the run first." }) : J(SignalLogic.ToDto(s));
    }

    [McpServerTool(Name = "update_signal"), Description(
        "Record the reader's decision on a signal: status (new | reviewed | acted | dismissed) and/or notes. " +
        "PATCH semantics: omit a field to keep it, pass an empty notes string to clear the notes. Normally the reader does " +
        "this in the tab; use it when they tell you their decision in conversation.")]
    public async Task<string> UpdateSignal(
        [Description("The signal's event_id.")] string event_id,
        [Description("new | reviewed | acted | dismissed")] string? status = null,
        [Description("Free text; empty string clears.")] string? notes = null)
    {
        var s = await db.Signals.FirstOrDefaultAsync(x => x.EventId == event_id.Trim());
        if (s is null) return J(new { error = $"No signal with event_id '{event_id}'." });
        var (updated, error) = await SignalLogic.UpdateAsync(db, s, status, notes);
        return error is not null ? J(new { error }) : J(SignalLogic.ToDto(updated!));
    }

    [McpServerTool(Name = "mark_signal_run_read"), Description("Tick a run as read. Omit the date for the latest run. The tab does this itself when a day is opened.")]
    public async Task<string> MarkSignalRunRead([Description("Run date (YYYY-MM-DD). Omit for the most recent run.")] string? date = null)
    {
        var run = await SignalLogic.FindRunAsync(db, date, track: true);
        if (run is null) return J(new { error = "No such run." });
        await SignalLogic.MarkReadAsync(db, run, true);
        return J(SignalLogic.ToSummary(run));
    }

    [McpServerTool(Name = "mark_signal_run_unread"), Description("Untick a run, so it shows as unread again.")]
    public async Task<string> MarkSignalRunUnread([Description("Run date (YYYY-MM-DD). Omit for the most recent run.")] string? date = null)
    {
        var run = await SignalLogic.FindRunAsync(db, date, track: true);
        if (run is null) return J(new { error = "No such run." });
        await SignalLogic.MarkReadAsync(db, run, false);
        return J(SignalLogic.ToSummary(run));
    }
}
