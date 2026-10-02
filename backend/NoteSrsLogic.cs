using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// Notes v2 — the single home for the prompt-scheduling rules, shared by the REST endpoints (Notes
/// tab) and the MCP tools (the quiz skill), so the two surfaces cannot drift on what "due", "parked"
/// or "today's cap" means. The scheduler itself is <see cref="Fsrs"/>; this class decides what is
/// asked today and in what order.
///
/// The design, and the simulations it rests on, in brief:
/// <list type="bullet">
/// <item><b>Prompts, not notes, are scheduled.</b> One fact per prompt so each gets an accurate grade.</item>
/// <item><b>Writing the note is the first exposure.</b> A new prompt starts as if graded Good and is due
/// in about four days. Intake is never limited; nothing new spends a slot of the cap.</item>
/// <item><b>The cap is on questions per day</b> (<see cref="DailyCap"/>), never on reviews per prompt. At
/// steady state each carried prompt costs about <see cref="ReviewsPerCarriedPrompt"/> questions a day,
/// so the cap carries roughly cap/7 new prompts a day; the Notes tab shows that number.</item>
/// <item><b>Triage inside the cap:</b> yesterday's lapses first, then due prompts by predicted recall,
/// highest first — a prompt at 85% is cheap to reinforce and leaps to a long interval; one at 30% is
/// mostly gone and costs the same to relearn next week.</item>
/// <item><b>Overflow that fell below <see cref="ParkBelow"/> is parked</b>, not deleted, and pulled back in
/// on a day with spare slots. Overflow above it just stays due.</item>
/// <item><b>Leeches are suspended</b> after <see cref="LeechLapses"/> lapses and flagged for rewriting:
/// repeated failure almost always means a badly formed prompt.</item>
/// </list>
/// </summary>
public static class NoteSrsLogic
{
    /// <summary>Review when predicted recall falls to this. 0.9 means the interval equals FSRS stability.</summary>
    public const double DesiredRetention = 0.9;

    /// <summary>Recorded reviews per calendar day (Asia/Yerevan), across both books.</summary>
    public const int DailyCap = 25;

    /// <summary>Due prompts that do not fit the cap and whose predicted recall is below this are parked.</summary>
    public const double ParkBelow = 0.5;

    /// <summary>Lapses after which a prompt is suspended as a leech.</summary>
    public const int LeechLapses = 8;

    /// <summary>Hard ceiling per note. The skill aims for one to three.</summary>
    public const int MaxPromptsPerNote = 5;

    /// <summary>Steady-state questions per day that one carried prompt costs at 0.9 retention (simulated over two years).</summary>
    public const double ReviewsPerCarriedPrompt = 7.0;

    /// <summary>
    /// Backfilled prompts (older notes entering v2 after the fact) are spread so that no day gets more than
    /// this many of their first reviews. 500 prompts arriving at once would otherwise all come due together.
    /// </summary>
    public const int BackfillPerDay = 10;

    public const int MaxQuestionLength = 1000;
    public const int MaxAnswerLength = 4000;

    public static double CarryCapacityPerDay => Math.Round(DailyCap / ReviewsPerCarriedPrompt, 1);

    // ===== Time =====

    public static DateOnly LastReviewDate(NotePrompt p) => AppClock.ToLocalDate(p.LastReviewedAt);

    public static int ElapsedDays(NotePrompt p, DateOnly today) =>
        Math.Max(0, today.DayNumber - LastReviewDate(p).DayNumber);

    public static double Retrievability(NotePrompt p, DateOnly today) =>
        Fsrs.Retrievability(p.Stability, ElapsedDays(p, today));

    public static bool IsDue(NotePrompt p, DateOnly today) => p.State == NotePromptState.Active && p.DueOn <= today;

    public static bool IsLeech(NotePrompt p) => p.State == NotePromptState.Suspended && p.Lapses >= LeechLapses;

    // ===== Parsing =====

    /// <summary>again | hard | good | easy, their FSRS numbers 1-4, or the aliases fail / pass / known / o.</summary>
    public static bool TryParseGrade(string? raw, out FsrsGrade grade)
    {
        grade = FsrsGrade.Good;
        var s = raw?.Trim().ToLowerInvariant();
        switch (s)
        {
            case "again" or "1" or "fail" or "wrong" or "forgot": grade = FsrsGrade.Again; return true;
            case "hard" or "2" or "partial": grade = FsrsGrade.Hard; return true;
            case "good" or "3" or "pass" or "correct": grade = FsrsGrade.Good; return true;
            case "easy" or "4" or "known" or "o": grade = FsrsGrade.Easy; return true;
            default: return false;
        }
    }

    public static bool TryParseState(string? raw, out NotePromptState state) =>
        Enum.TryParse(raw?.Trim(), ignoreCase: true, out state);

    /// <summary>Null when the pair is acceptable, otherwise the reason it is not.</summary>
    public static string? ValidatePrompt(string? question, string? answer)
    {
        if (string.IsNullOrWhiteSpace(question)) return "question is required";
        if (string.IsNullOrWhiteSpace(answer)) return "answer is required";
        if (question.Trim().Length > MaxQuestionLength) return $"question is longer than {MaxQuestionLength} characters";
        if (answer.Trim().Length > MaxAnswerLength) return $"answer is longer than {MaxAnswerLength} characters";
        return null;
    }

    // ===== Lifecycle =====

    /// <summary>
    /// A prompt fresh from its note: the state a Good first rating gives, due in ~4 days, with creation
    /// standing in for the first review. Not attached — the caller adds it.
    /// </summary>
    public static NotePrompt NewPrompt(Note note, string question, string answer, int sortOrder, DateTime nowUtc, DateOnly today)
    {
        var p = new NotePrompt
        {
            Id = Guid.NewGuid(),
            NoteId = note.Id,
            Question = question.Trim(),
            Answer = answer.Trim(),
            SortOrder = sortOrder,
            CreatedAt = nowUtc,
        };
        ResetSchedule(p, nowUtc, today);
        return p;
    }

    /// <summary>Restart the schedule as if the prompt had just been written. For a rewritten leech.</summary>
    public static void ResetSchedule(NotePrompt p, DateTime nowUtc, DateOnly today)
    {
        var initial = Fsrs.Initial(FsrsGrade.Good);
        p.State = NotePromptState.Active;
        p.Difficulty = initial.Difficulty;
        p.Stability = initial.Stability;
        p.DueOn = today.AddDays(Fsrs.IntervalDays(initial.Stability, DesiredRetention));
        p.LastReviewedAt = nowUtc;
        p.Relearning = false;
        p.Lapses = 0;
        p.Reviews = 0;
        p.UpdatedAt = nowUtc;
    }

    /// <summary>
    /// Apply a graded review: move the FSRS state, stamp the next due day and build the immutable
    /// review row. Not attached and not saved — the caller does <c>db.NotePromptReviews.Add(review)</c>
    /// (adding through the navigation would make EF treat the pre-keyed row as Modified; see VocabStore).
    ///
    /// A lapse comes back tomorrow, flagged relearning so it leads tomorrow's queue, and the eighth lapse
    /// suspends the prompt. A pass is due again when predicted recall will have fallen to
    /// <see cref="DesiredRetention"/>. Reviewing a parked prompt re-activates it.
    /// </summary>
    public static NotePromptReview ApplyReview(NotePrompt p, FsrsGrade grade, string? answer, string? note, DateOnly today, DateTime nowUtc)
    {
        var elapsed = ElapsedDays(p, today);
        var r = Fsrs.Retrievability(p.Stability, elapsed);
        var before = new Fsrs.State(p.Difficulty, p.Stability);
        var after = Fsrs.Next(before, r, grade);

        var review = new NotePromptReview
        {
            Id = Guid.NewGuid(),
            NotePromptId = p.Id,
            ReviewedAt = nowUtc,
            ReviewDate = today,
            Grade = (int)grade,
            ElapsedDays = elapsed,
            Retrievability = r,
            WasRelearning = p.Relearning,
            StabilityBefore = before.Stability,
            StabilityAfter = after.Stability,
            DifficultyBefore = before.Difficulty,
            DifficultyAfter = after.Difficulty,
            Answer = answer,
            Note = note,
        };

        p.Difficulty = after.Difficulty;
        p.Stability = after.Stability;
        p.LastReviewedAt = nowUtc;
        p.Reviews++;
        p.UpdatedAt = nowUtc;
        if (p.State == NotePromptState.Parked) p.State = NotePromptState.Active;

        if (grade == FsrsGrade.Again)
        {
            p.Lapses++;
            p.Relearning = true;
            p.DueOn = today.AddDays(1);
            if (p.Lapses >= LeechLapses) p.State = NotePromptState.Suspended;
        }
        else
        {
            p.Relearning = false;
            p.DueOn = today.AddDays(Fsrs.IntervalDays(after.Stability, DesiredRetention));
        }

        return review;
    }

    /// <summary>
    /// Add prompts to a note. Validates each pair, refuses duplicates of a question already on the note
    /// and refuses to exceed <see cref="MaxPromptsPerNote"/> — never truncates silently. Adds to the
    /// context but does not save. Returns the new rows, or an error.
    /// </summary>
    public static async Task<(List<NotePrompt> Created, string? Error)> CreatePromptsAsync(
        RoadmapDbContext db, Note note, IReadOnlyList<NotePromptInput>? inputs, bool backfill = false)
    {
        if (inputs is null || inputs.Count == 0) return ([], "prompts is required — at least one { question, answer }");

        var existing = await db.NotePrompts.Where(p => p.NoteId == note.Id).ToListAsync();
        if (existing.Count + inputs.Count > MaxPromptsPerNote)
            return ([], $"a note holds at most {MaxPromptsPerNote} prompts; this one has {existing.Count} and {inputs.Count} more were given");

        var seen = new HashSet<string>(existing.Select(p => p.Question.Trim().ToLowerInvariant()));
        var nowUtc = DateTime.UtcNow;
        var today = AppClock.Today();
        var sort = existing.Count == 0 ? 0 : existing.Max(p => p.SortOrder) + 1;

        // Backfill: the note was the exposure, back on its own date — so a prompt the learner still
        // recalls after months leaps to a long interval on its first pass, and one they have lost comes
        // back tomorrow. Its first review lands on the first day from the normal +4 that still has room.
        Dictionary<DateOnly, int>? dueCounts = null;
        var firstDay = today.AddDays(Fsrs.IntervalDays(Fsrs.Initial(FsrsGrade.Good).Stability, DesiredRetention));
        if (backfill)
        {
            dueCounts = await db.NotePrompts
                .Where(p => p.State == NotePromptState.Active && p.DueOn >= firstDay)
                .GroupBy(p => p.DueOn).Select(g => new { Day = g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.Day, x => x.N);
        }

        var created = new List<NotePrompt>();
        foreach (var input in inputs)
        {
            var err = ValidatePrompt(input.Question, input.Answer);
            if (err is not null) return ([], err);
            var key = input.Question.Trim().ToLowerInvariant();
            if (!seen.Add(key)) return ([], $"duplicate question: \"{input.Question.Trim()}\"");
            var p = NewPrompt(note, input.Question, input.Answer, sort++, nowUtc, today);
            if (backfill && dueCounts is not null)
            {
                var exposure = AppClock.StartOfDayUtc(note.EntryDate);
                p.LastReviewedAt = exposure < nowUtc ? exposure : nowUtc;
                var day = firstDay;
                while (dueCounts.GetValueOrDefault(day) >= BackfillPerDay) day = day.AddDays(1);
                dueCounts[day] = dueCounts.GetValueOrDefault(day) + 1;
                p.DueOn = day;
            }
            p.Note = note;
            db.NotePrompts.Add(p);
            created.Add(p);
        }
        return (created, null);
    }

    /// <summary>
    /// PATCH a prompt: null keeps a field. <paramref name="state"/> is Active | Parked | Suspended;
    /// <paramref name="reset"/> restarts the schedule (a rewritten leech starts fresh). Returns an error or null.
    /// </summary>
    public static string? ApplyUpdate(NotePrompt p, string? question, string? answer, string? state, bool reset)
    {
        var q = question is null ? p.Question : question.Trim();
        var a = answer is null ? p.Answer : answer.Trim();
        var err = ValidatePrompt(q, a);
        if (err is not null) return err;

        NotePromptState? newState = null;
        if (state is not null)
        {
            if (!TryParseState(state, out var parsed)) return $"invalid state '{state}'. Use Active | Parked | Suspended.";
            newState = parsed;
        }

        var nowUtc = DateTime.UtcNow;
        p.Question = q;
        p.Answer = a;
        if (reset) ResetSchedule(p, nowUtc, AppClock.Today());
        if (newState is { } ns) p.State = ns;
        p.UpdatedAt = nowUtc;
        return null;
    }

    // ===== Queries =====

    public static Task<int> AskedTodayAsync(RoadmapDbContext db, DateOnly today) =>
        db.NotePromptReviews.CountAsync(r => r.ReviewDate == today);

    /// <summary>
    /// Today's queue under the cap. <paramref name="persist"/> false (the tab's read-only preview) computes
    /// the same queue without parking or unparking anything; the MCP session passes true.
    /// </summary>
    public static async Task<NoteSrsSessionDto> BuildSessionAsync(RoadmapDbContext db, string? book, bool persist)
    {
        var today = AppClock.Today();
        var nowUtc = DateTime.UtcNow;
        var askedToday = await AskedTodayAsync(db, today);
        var remaining = Math.Max(0, DailyCap - askedToday);

        IQueryable<NotePrompt> q = db.NotePrompts.Include(p => p.Note)
            .Where(p => (p.State == NotePromptState.Active && p.DueOn <= today) || p.State == NotePromptState.Parked);
        if (book is not null) q = q.Where(p => p.Note!.Book == book);
        if (!persist) q = q.AsNoTracking();
        var candidates = await q.ToListAsync();

        var due = candidates.Where(p => p.State == NotePromptState.Active).ToList();
        var parked = candidates.Where(p => p.State == NotePromptState.Parked).ToList();

        // Lapses from yesterday lead; then the ones most worth saving — highest predicted recall first.
        var queue = due.Where(p => p.Relearning).OrderBy(p => p.DueOn).ThenBy(p => p.CreatedAt)
            .Concat(due.Where(p => !p.Relearning)
                .OrderByDescending(p => Retrievability(p, today)).ThenBy(p => p.DueOn).ThenBy(p => p.CreatedAt))
            .ToList();

        var taken = queue.Take(remaining).ToList();
        var overflow = queue.Skip(remaining).ToList();

        var parkedNow = 0;
        foreach (var p in overflow.Where(p => !p.Relearning && Retrievability(p, today) < ParkBelow))
        {
            if (persist) { p.State = NotePromptState.Parked; p.UpdatedAt = nowUtc; }
            parkedNow++;
        }

        var unparked = new List<NotePrompt>();
        var spare = remaining - taken.Count;
        if (spare > 0)
        {
            foreach (var p in parked.OrderByDescending(p => Retrievability(p, today)).ThenBy(p => p.DueOn).Take(spare))
            {
                if (persist) { p.State = NotePromptState.Active; p.UpdatedAt = nowUtc; }
                unparked.Add(p);
            }
        }

        if (persist) await db.SaveChangesAsync();

        var prompts = taken.Concat(unparked).Select(p => ToDto(p, today, includeHistory: false)).ToList();
        return new NoteSrsSessionDto(
            Date: today.ToString("yyyy-MM-dd"),
            DailyCap: DailyCap,
            AskedToday: askedToday,
            Remaining: remaining,
            DueTotal: due.Count,
            Returned: prompts.Count,
            Overflow: overflow.Count,
            ParkedNow: parkedNow,
            Unparked: unparked.Count,
            ParkedTotal: parked.Count - unparked.Count + parkedNow,
            CarryCapacityPerDay: CarryCapacityPerDay,
            DesiredRetention: DesiredRetention,
            Prompts: prompts);
    }

    public static async Task<NoteSrsStatsDto> StatsAsync(RoadmapDbContext db)
    {
        var today = AppClock.Today();
        var prompts = await db.NotePrompts.AsNoTracking()
            .Select(p => new { p.NoteId, p.State, p.DueOn, p.Stability, p.Lapses })
            .ToListAsync();
        var noteCount = await db.Notes.CountAsync();
        var notesWith = prompts.Select(p => p.NoteId).Distinct().Count();
        var askedToday = await AskedTodayAsync(db, today);

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var monthAgo = today.AddDays(-30);
        var reviewsAll = await db.NotePromptReviews.CountAsync();
        var reviews7 = await db.NotePromptReviews.CountAsync(r => r.ReviewedAt >= weekAgo);
        // True retention: the share of scheduled (non-relearning) reviews that passed. Should hover near
        // DesiredRetention; drifting away means the parameters want re-fitting.
        var recent = await db.NotePromptReviews.AsNoTracking()
            .Where(r => r.ReviewDate >= monthAgo && !r.WasRelearning)
            .Select(r => r.Grade).ToListAsync();
        double? trueRetention = recent.Count == 0 ? null : Math.Round(recent.Count(g => g > (int)FsrsGrade.Again) / (double)recent.Count, 3);

        var active = prompts.Where(p => p.State == NotePromptState.Active).ToList();
        var dueToday = active.Count(p => p.DueOn <= today);
        var load = Enumerable.Range(0, 7).Select(i =>
        {
            var d = today.AddDays(i);
            return new DayLoadDto(d.ToString("yyyy-MM-dd"), i == 0 ? dueToday : active.Count(p => p.DueOn == d));
        }).ToList();

        return new NoteSrsStatsDto(
            Prompts: prompts.Count,
            Active: active.Count,
            Parked: prompts.Count(p => p.State == NotePromptState.Parked),
            Suspended: prompts.Count(p => p.State == NotePromptState.Suspended),
            Leeches: prompts.Count(p => p.State == NotePromptState.Suspended && p.Lapses >= LeechLapses),
            NotesWithPrompts: notesWith,
            NotesWithoutPrompts: Math.Max(0, noteCount - notesWith),
            DueToday: dueToday,
            AskedToday: askedToday,
            DailyCap: DailyCap,
            Remaining: Math.Max(0, DailyCap - askedToday),
            CarryCapacityPerDay: CarryCapacityPerDay,
            DesiredRetention: DesiredRetention,
            ReviewsAllTime: reviewsAll,
            ReviewsLast7Days: reviews7,
            TrueRetention30d: trueRetention,
            Lapses: prompts.Sum(p => p.Lapses),
            AverageStability: prompts.Count == 0 ? 0 : Math.Round(prompts.Average(p => p.Stability), 1),
            UpcomingLoad: load);
    }

    /// <summary>Per-note counts for one book, for the Notes tab's day list.</summary>
    public static async Task<List<NotePromptOverviewDto>> OverviewAsync(RoadmapDbContext db, string book)
    {
        var today = AppClock.Today();
        var rows = await db.NotePrompts.AsNoTracking()
            .Where(p => p.Note!.Book == book)
            .Select(p => new { p.Note!.DayNumber, p.State, p.DueOn })
            .ToListAsync();
        return rows.GroupBy(r => r.DayNumber)
            .Select(g => new NotePromptOverviewDto(
                g.Key,
                g.Count(),
                g.Count(x => x.State == NotePromptState.Active && x.DueOn <= today),
                g.Count(x => x.State == NotePromptState.Parked),
                g.Count(x => x.State == NotePromptState.Suspended),
                g.Where(x => x.State == NotePromptState.Active).Select(x => (DateOnly?)x.DueOn).Min()?.ToString("yyyy-MM-dd")))
            .OrderByDescending(o => o.DayNumber)
            .ToList();
    }

    // ===== DTOs =====

    public static NotePromptDto ToDto(NotePrompt p, DateOnly today, bool includeHistory) => new(
        p.Id,
        p.Note?.Book ?? "",
        p.Note?.DayNumber ?? 0,
        p.Note?.EntryDate.ToString("yyyy-MM-dd") ?? "",
        p.Question, p.Answer, p.SortOrder,
        p.State.ToString(),
        Math.Round(p.Difficulty, 2),
        Math.Round(p.Stability, 1),
        Math.Round(Retrievability(p, today), 3),
        p.DueOn.ToString("yyyy-MM-dd"),
        IsDue(p, today),
        p.Relearning, p.Lapses, p.Reviews, p.LastReviewedAt, p.CreatedAt,
        includeHistory
            ? p.ReviewHistory.OrderByDescending(r => r.ReviewedAt).Select(r => new NotePromptReviewDto(
                r.ReviewedAt, ((FsrsGrade)r.Grade).ToString(), r.ElapsedDays, Math.Round(r.Retrievability, 3),
                Math.Round(r.StabilityBefore, 1), Math.Round(r.StabilityAfter, 1),
                Math.Round(r.DifficultyBefore, 2), Math.Round(r.DifficultyAfter, 2),
                r.WasRelearning, r.Answer, r.Note)).ToList()
            : []);
}
