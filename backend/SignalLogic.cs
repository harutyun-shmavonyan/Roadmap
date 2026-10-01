using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// The rules of the Stock Signals tab, shared by the REST endpoints and the MCP tools so the two doors
/// cannot drift: what a publish replaces and what it carries across, when a day counts as read, how long
/// a quiet day is kept, and the wire shape of a run.
/// </summary>
public static class SignalLogic
{
    /// <summary>A quiet day older than this is pruned on the next publish. Days that fired are never pruned.</summary>
    public const int QuietRetentionDays = 120;

    public static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase)
        { "new", "reviewed", "acted", "dismissed" };
    private static readonly HashSet<string> RunStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "OK", "DEGRADED", "FAILED" };
    private static readonly HashSet<string> Screens = new(StringComparer.OrdinalIgnoreCase) { "A", "B", "C" };

    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    // ── reading input ──

    public static bool TryParseDate(string? raw, out DateOnly date)
    {
        date = default;
        return !string.IsNullOrWhiteSpace(raw) && DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Null when the input is publishable, else the reason it is not. Nothing is written on an error.</summary>
    public static string? Validate(PublishSignalRunInput input)
    {
        if (!TryParseDate(input.run_date, out _)) return "run_date is required, as YYYY-MM-DD.";
        if (!string.IsNullOrWhiteSpace(input.status) && !RunStatuses.Contains(input.status.Trim()))
            return "status must be OK, DEGRADED or FAILED.";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in input.signals ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.event_id)) return "Every signal needs an event_id.";
            if (!seen.Add(s.event_id.Trim())) return $"event_id '{s.event_id}' appears twice.";
            if (string.IsNullOrWhiteSpace(s.screen) || !Screens.Contains(s.screen.Trim())) return $"Signal '{s.event_id}': screen must be A, B or C.";
            if (string.IsNullOrWhiteSpace(s.key)) return $"Signal '{s.event_id}': key is required (the group, the benchmark, or 'list').";
            if (s.evidence is { ValueKind: not (JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined) })
                return $"Signal '{s.event_id}': evidence must be a JSON array.";
            if (s.candidates is { ValueKind: not (JsonValueKind.Array or JsonValueKind.Null or JsonValueKind.Undefined) })
                return $"Signal '{s.event_id}': candidates must be a JSON array.";
        }
        return null;
    }

    // ── publishing ──

    /// <summary>
    /// Store a run, replacing the one already published for that date.
    ///
    /// What the screener owns is replaced: the signals, their evidence, the context, the report. What the
    /// screener does not own is carried across by <c>event_id</c>: the reader's status and notes on each
    /// signal, and the triage verdict the skill wrote for it. A re-publish whose set of signals is unchanged
    /// keeps the day's read tick; one that adds or removes a signal makes the day unread again, because
    /// there is something new to look at. Quiet days older than <see cref="QuietRetentionDays"/> go.
    /// </summary>
    public static async Task<(SignalRun Run, string Action, int Carried, int Pruned)> PublishAsync(
        RoadmapDbContext db, PublishSignalRunInput input)
    {
        var error = Validate(input);
        if (error is not null) throw new ArgumentException(error);
        TryParseDate(input.run_date, out var date);

        var existing = await db.SignalRuns.Include(r => r.Signals).FirstOrDefaultAsync(r => r.RunDate == date);
        var action = existing is null ? "created" : "replaced";
        var now = DateTime.UtcNow;

        var prior = (existing?.Signals ?? []).ToDictionary(s => s.EventId, StringComparer.OrdinalIgnoreCase);
        var inputs = input.signals ?? [];
        var newIds = inputs.Select(s => s.event_id!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sameSet = existing is not null && prior.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(newIds);

        SignalRun run;
        if (existing is not null)
        {
            db.Signals.RemoveRange(existing.Signals);
            run = existing;
            if (!sameSet) { run.IsRead = false; run.ReadOn = null; }
        }
        else
        {
            run = new SignalRun { Id = Guid.NewGuid(), RunDate = date, CreatedAt = now };
            db.SignalRuns.Add(run);
        }

        run.Status = string.IsNullOrWhiteSpace(input.status) ? "OK" : input.status.Trim().ToUpperInvariant();
        run.IsTradingDay = input.is_trading_day ?? true;
        run.MarketsJson = Store(input.markets);
        run.WatchJson = Store(input.watch);
        run.Warnings = [.. (input.warnings ?? []).Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim())];
        run.ReportMarkdown = string.IsNullOrWhiteSpace(input.report_markdown) ? null : input.report_markdown;
        run.GitSha = string.IsNullOrWhiteSpace(input.git_sha) ? null : Trunc(input.git_sha.Trim(), 64);
        run.SignalCount = inputs.Length;
        run.Summary = Trunc(string.IsNullOrWhiteSpace(input.summary) ? DefaultSummary(run, inputs) : input.summary.Trim(), 512);
        run.PublishedAt = now;
        run.UpdatedAt = now;

        var carried = 0;
        var order = 0;
        foreach (var s in inputs)
        {
            var eventId = s.event_id!.Trim();
            prior.TryGetValue(eventId, out var old);
            if (old is not null && (old.Status != "new" || old.Notes is not null || old.TriageJson is not null)) carried++;
            var screen = s.screen!.Trim().ToUpperInvariant();
            var market = string.IsNullOrWhiteSpace(s.market) ? "US" : s.market.Trim().ToUpperInvariant();
            db.Signals.Add(new Signal
            {
                Id = Guid.NewGuid(),
                SignalRunId = run.Id,
                EventId = Trunc(eventId, 160),
                Market = Trunc(market, 8),
                Screen = screen,
                Key = Trunc(s.key!.Trim(), 160),
                Tier = s.tier ?? 1,
                Title = Trunc(string.IsNullOrWhiteSpace(s.title) ? DefaultTitle(market, screen, s.key!, s.tier ?? 1) : s.title.Trim(), 256),
                Headline = Trunc((s.headline ?? "").Trim(), 1024),
                Thesis = (s.thesis ?? "").Trim(),
                EvidenceJson = Store(s.evidence) ?? "[]",
                Horizon = (s.horizon ?? "").Trim(),
                HorizonDays = s.horizon_days,
                Proposal = string.IsNullOrWhiteSpace(s.proposal) ? null : s.proposal.Trim(),
                ProposalJson = Store(s.proposal_detail),
                CandidatesJson = Store(s.candidates) ?? "[]",
                ContextJson = Store(s.context),
                Invalidation = string.IsNullOrWhiteSpace(s.invalidation) ? null : s.invalidation.Trim(),
                Regime = string.IsNullOrWhiteSpace(s.regime) ? null : Trunc(s.regime.Trim(), 16),
                NextStep = string.IsNullOrWhiteSpace(s.next_step) ? null : s.next_step.Trim(),
                SortOrder = order++,
                // layers 2 and 3 belong to the skill and the reader, not the screener: they ride across
                TriageJson = old?.TriageJson,
                TriageSummary = old?.TriageSummary,
                TriagedAt = old?.TriagedAt,
                TriageModel = old?.TriageModel,
                Status = old?.Status ?? "new",
                Notes = old?.Notes,
                DecidedAt = old?.DecidedAt,
                CreatedAt = old?.CreatedAt ?? now,
                UpdatedAt = now,
            });
        }

        var oldest = AppClock.Today().AddDays(-QuietRetentionDays);
        var stale = await db.SignalRuns.Where(r => r.SignalCount == 0 && r.RunDate < oldest && r.Id != run.Id).ToListAsync();
        db.SignalRuns.RemoveRange(stale);

        await db.SaveChangesAsync();
        return (run, action, carried, stale.Count);
    }

    /// <summary>The triage skill's verdict for one signal. New content, so the day comes back unread.</summary>
    public static async Task<Signal?> SetTriageAsync(RoadmapDbContext db, string eventId, JsonElement? triage,
        string? summary, string? model)
    {
        var s = await db.Signals.Include(x => x.Run).FirstOrDefaultAsync(x => x.EventId == eventId.Trim());
        if (s is null) return null;
        var now = DateTime.UtcNow;
        s.TriageJson = Store(triage) ?? s.TriageJson;
        s.TriageSummary = string.IsNullOrWhiteSpace(summary) ? s.TriageSummary : summary.Trim();
        s.TriageModel = string.IsNullOrWhiteSpace(model) ? s.TriageModel : Trunc(model.Trim(), 64);
        s.TriagedAt = now;
        s.UpdatedAt = now;
        s.Run.IsRead = false;
        s.Run.ReadOn = null;
        s.Run.UpdatedAt = now;
        await db.SaveChangesAsync();
        return s;
    }

    /// <summary>The reader's decision. PATCH semantics: null keeps, an empty notes string clears.</summary>
    public static async Task<(Signal? Signal, string? Error)> UpdateAsync(RoadmapDbContext db, Signal s, string? status, string? notes)
    {
        if (status is not null)
        {
            var st = status.Trim().ToLowerInvariant();
            if (!Statuses.Contains(st)) return (null, "status must be new, reviewed, acted or dismissed.");
            if (st != s.Status)
            {
                s.Status = st;
                s.DecidedAt = st == "new" ? null : DateTime.UtcNow;
            }
        }
        if (notes is not null) s.Notes = notes.Trim().Length == 0 ? null : notes.Trim();
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (s, null);
    }

    public static async Task<SignalRun?> FindRunAsync(RoadmapDbContext db, string? date, bool track = false)
    {
        var q = track ? db.SignalRuns.Include(r => r.Signals) : db.SignalRuns.AsNoTracking().Include(r => r.Signals);
        if (string.IsNullOrWhiteSpace(date)) return await q.OrderByDescending(r => r.RunDate).FirstOrDefaultAsync();
        return TryParseDate(date, out var d) ? await q.FirstOrDefaultAsync(r => r.RunDate == d) : null;
    }

    public static async Task MarkReadAsync(RoadmapDbContext db, SignalRun run, bool read)
    {
        run.IsRead = read;
        run.ReadOn = read ? AppClock.Today() : null;
        run.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // ── wire shapes ──

    public static SignalRunSummaryDto ToSummary(SignalRun r) => new(
        r.Id, r.RunDate.ToString("yyyy-MM-dd"), r.Status, r.IsTradingDay, r.Summary,
        r.SignalCount, CountArray(r.WatchJson), r.IsRead, r.ReadOn?.ToString("yyyy-MM-dd"), r.PublishedAt, r.GitSha);

    public static SignalRunDto ToDto(SignalRun r) => new(
        r.Id, r.RunDate.ToString("yyyy-MM-dd"), r.Status, r.IsTradingDay, r.Summary,
        Load(r.MarketsJson), Load(r.WatchJson), r.Warnings, r.ReportMarkdown, r.GitSha,
        r.IsRead, r.ReadOn?.ToString("yyyy-MM-dd"), r.PublishedAt, r.CreatedAt, r.UpdatedAt,
        r.Signals.OrderBy(s => s.SortOrder).Select(ToDto).ToList());

    public static SignalDto ToDto(Signal s) => new(
        s.Id, s.EventId, s.Market, s.Screen, s.Key, s.Tier, s.Title, s.Headline, s.Thesis,
        Load(s.EvidenceJson) ?? EmptyArray, s.Horizon, s.HorizonDays, s.Proposal, Load(s.ProposalJson),
        Load(s.CandidatesJson) ?? EmptyArray, Load(s.ContextJson), s.Invalidation, s.Regime, s.NextStep, s.SortOrder,
        Load(s.TriageJson), s.TriageSummary, s.TriagedAt, s.TriageModel,
        s.Status, s.Notes, s.DecidedAt, s.UpdatedAt);

    private static readonly JsonElement EmptyArray = JsonDocument.Parse("[]").RootElement.Clone();

    // ── helpers ──

    private static string DefaultSummary(SignalRun run, SignalInput[] signals)
    {
        if (!run.IsTradingDay) return "Not a trading day";
        if (signals.Length == 0) return run.Status == "OK" ? "No important signal" : $"No important signal ({run.Status.ToLowerInvariant()} run)";
        var names = signals.Select(s => string.IsNullOrWhiteSpace(s.title) ? s.key : s.title).Take(3);
        return $"{signals.Length} signal{(signals.Length == 1 ? "" : "s")}: {string.Join("; ", names)}";
    }

    private static string DefaultTitle(string market, string screen, string key, int tier) => screen switch
    {
        "A" => $"[{market}] {key} — sector panic, tier {tier}",
        "B" => $"[{market}] index panic, tier {tier}",
        _ => $"[{market}] cyclical upturn list",
    };

    /// <summary>JSON stored as the text the caller sent, minus whitespace; null/undefined stores nothing.</summary>
    private static string? Store(JsonElement? e) =>
        e is null || e.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : JsonSerializer.Serialize(e.Value, Compact);

    private static JsonElement? Load(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json).RootElement.Clone(); }
        catch (JsonException) { return null; }
    }

    private static int CountArray(string? json)
    {
        var e = Load(json);
        return e is { ValueKind: JsonValueKind.Array } ? e.Value.GetArrayLength() : 0;
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max];
}
