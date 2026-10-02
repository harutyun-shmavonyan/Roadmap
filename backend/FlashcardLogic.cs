using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// Notes v2 — the single home for the flashcard rules, shared by the REST endpoints (Notes v2 tab)
/// and the MCP tools (the quiz and note-taking skills), so the two surfaces cannot drift on what
/// "due", "parked" or "today's cap" means. The scheduler itself is <see cref="Fsrs"/>; this class
/// decides what is asked today and in what order. Nothing here touches the v1 <c>notes</c> table.
///
/// The design, and the simulations it rests on, in brief:
/// <list type="bullet">
/// <item><b>A card is one note; prompts, not cards, are scheduled.</b> One fact per prompt so each gets
/// an accurate grade; a card has as many prompts as its subpoints need. The date is only a filter.</item>
/// <item><b>Writing the card is the first exposure.</b> A new prompt starts as if graded Good and is due
/// in about four days. Intake is never limited; nothing new spends a slot of the cap.</item>
/// <item><b>Red and green are two separate worlds.</b> Every session, cap, stat and dashboard belongs to one
/// book; nothing here ever aggregates across them, so either book could be dropped on its own.</item>
/// <item><b>The cap is on questions per day, per book</b> (<see cref="DailyCap"/>), never on reviews per prompt. At
/// steady state each carried prompt costs about <see cref="ReviewsPerCarriedPrompt"/> questions a day,
/// so the cap carries roughly cap/7 new prompts a day; the tab shows that number.</item>
/// <item><b>Triage inside the cap:</b> yesterday's lapses first, then due prompts by predicted recall,
/// highest first — a prompt at 85% is cheap to reinforce and leaps to a long interval; one at 30% is
/// mostly gone and costs the same to relearn next week.</item>
/// <item><b>Reviewed overflow that fell below <see cref="ParkBelow"/> is parked</b>, not deleted, and pulled
/// back in on a day with spare slots. Never-answered prompts are never parked: a backlog of first answers
/// stays visibly due and is worked through under the cap, most recallable first.</item>
/// <item><b>A backdated card is due when it would have been:</b> its own date is the exposure, so its
/// first review is that date + 4 days — today, for anything older than that. No spreading: the cap and
/// the triage order pace a backlog, and the due count shows it shrinking day by day.</item>
/// <item><b>Leeches are suspended</b> after <see cref="LeechLapses"/> lapses and flagged for rewriting:
/// repeated failure almost always means a badly formed prompt.</item>
/// </list>
/// </summary>
public static class FlashcardLogic
{
    /// <summary>Review when predicted recall falls to this. 0.9 means the interval equals FSRS stability.</summary>
    public const double DesiredRetention = 0.9;

    /// <summary>Recorded reviews per calendar day (Asia/Yerevan), per book — red and green each have their own.</summary>
    public const int DailyCap = 25;

    /// <summary>Due prompts that do not fit the cap and whose predicted recall is below this are parked.</summary>
    public const double ParkBelow = 0.5;

    /// <summary>Lapses after which a prompt is suspended as a leech.</summary>
    public const int LeechLapses = 8;

    /// <summary>A sanity ceiling only — a card has as many prompts as its subpoints need.</summary>
    public const int MaxPromptsPerCard = 50;

    /// <summary>Steady-state questions per day that one carried prompt costs at 0.9 retention (simulated over two years).</summary>
    public const double ReviewsPerCarriedPrompt = 7.0;

    public const int MaxQuestionLength = 1000;
    public const int MaxAnswerLength = 4000;

    public static double CarryCapacityPerDay => Math.Round(DailyCap / ReviewsPerCarriedPrompt, 1);

    // ===== Books =====

    public static bool TryBook(string? book, out string normalized)
    {
        normalized = book?.Trim().ToLowerInvariant() ?? "";
        return normalized is "red" or "green";
    }

    // ===== Time =====

    public static DateOnly LastReviewDate(FlashcardPrompt p) => AppClock.ToLocalDate(p.LastReviewedAt);

    public static int ElapsedDays(FlashcardPrompt p, DateOnly today) =>
        Math.Max(0, today.DayNumber - LastReviewDate(p).DayNumber);

    public static double Retrievability(FlashcardPrompt p, DateOnly today) =>
        Fsrs.Retrievability(p.Stability, ElapsedDays(p, today));

    public static bool IsDue(FlashcardPrompt p, DateOnly today) => p.State == FlashcardPromptState.Active && p.DueOn <= today;

    public static bool IsLeech(FlashcardPrompt p) => p.State == FlashcardPromptState.Suspended && p.Lapses >= LeechLapses;

    // ===== Parsing =====

    /// <summary>again | hard | good | easy, their FSRS numbers 1-4, or the aliases fail / pass / known / o.</summary>
    public static bool TryParseGrade(string? raw, out FsrsGrade grade)
    {
        grade = FsrsGrade.Good;
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "again" or "1" or "fail" or "wrong" or "forgot": grade = FsrsGrade.Again; return true;
            case "hard" or "2" or "partial": grade = FsrsGrade.Hard; return true;
            case "good" or "3" or "pass" or "correct": grade = FsrsGrade.Good; return true;
            case "easy" or "4" or "known" or "o": grade = FsrsGrade.Easy; return true;
            default: return false;
        }
    }

    public static bool TryParseState(string? raw, out FlashcardPromptState state) =>
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

    // ===== Cards =====

    /// <summary>Create a new card — one note. Never appends to another card. Adds and saves.</summary>
    public static async Task<Flashcard> CreateCardAsync(RoadmapDbContext db, string book, string content, DateOnly date)
    {
        var card = new Flashcard { Id = Guid.NewGuid(), Book = book, EntryDate = date, Content = content.Trim() };
        db.Flashcards.Add(card);
        await db.SaveChangesAsync();
        return card;
    }

    /// <summary>
    /// Split a card into several cards, each with its own content and the prompts listed for it. Every
    /// prompt of the original must be listed exactly once; prompts keep their id, schedule and review
    /// history (only their card changes). The new cards keep the original's book and date, and their
    /// creation order follows the order of the parts. The original card is deleted. Saves, in one
    /// transaction. Returns the new cards or an error.
    /// </summary>
    public static async Task<(List<Flashcard> Cards, string? Error)> SplitCardAsync(RoadmapDbContext db, Guid cardId, IReadOnlyList<FlashcardSplitPart>? parts)
    {
        var card = await db.Flashcards.Include(c => c.Prompts).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return ([], "flashcard not found");
        if (parts is null || parts.Count < 2) return ([], "a split needs at least two parts");
        if (parts.Any(p => string.IsNullOrWhiteSpace(p.Content))) return ([], "every part needs content");

        var owned = card.Prompts.Select(p => p.Id).ToHashSet();
        var listed = parts.SelectMany(p => p.PromptIds ?? []).ToList();
        var unknown = listed.Where(id => !owned.Contains(id)).ToList();
        if (unknown.Count > 0) return ([], $"prompt(s) not on this card: {string.Join(", ", unknown)}");
        var dupes = listed.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupes.Count > 0) return ([], $"prompt(s) assigned to more than one part: {string.Join(", ", dupes)}");
        var missing = owned.Except(listed).ToList();
        if (missing.Count > 0) return ([], $"every prompt must be assigned to a part; unassigned: {string.Join(", ", missing)}");

        await using var tx = await db.Database.BeginTransactionAsync();
        var byId = card.Prompts.ToDictionary(p => p.Id);
        var nowUtc = DateTime.UtcNow;
        var created = new List<Flashcard>();
        for (var i = 0; i < parts.Count; i++)
        {
            var part = new Flashcard
            {
                Id = Guid.NewGuid(), Book = card.Book, EntryDate = card.EntryDate, Content = parts[i].Content.Trim(),
                CreatedAt = card.CreatedAt.AddMilliseconds(i), UpdatedAt = nowUtc,
            };
            db.Flashcards.Add(part);
            var sort = 0;
            foreach (var id in parts[i].PromptIds ?? [])
            {
                var p = byId[id];
                p.FlashcardId = part.Id;
                p.Flashcard = part;
                p.SortOrder = sort++;
                p.UpdatedAt = nowUtc;
            }
            created.Add(part);
        }
        await db.SaveChangesAsync();
        card.Prompts.Clear();
        db.Flashcards.Remove(card);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (created, null);
    }

    // ===== Prompt lifecycle =====

    /// <summary>A prompt fresh from its card: the state a Good first rating gives, due in ~4 days. Not attached.</summary>
    public static FlashcardPrompt NewPrompt(Flashcard card, string question, string answer, int sortOrder, DateTime nowUtc, DateOnly today)
    {
        var p = new FlashcardPrompt
        {
            Id = Guid.NewGuid(),
            FlashcardId = card.Id,
            Question = question.Trim(),
            Answer = answer.Trim(),
            SortOrder = sortOrder,
            CreatedAt = nowUtc,
        };
        ResetSchedule(p, nowUtc, today);
        return p;
    }

    /// <summary>Restart the schedule as if the prompt had just been written. For a rewritten leech.</summary>
    public static void ResetSchedule(FlashcardPrompt p, DateTime nowUtc, DateOnly today)
    {
        var initial = Fsrs.Initial(FsrsGrade.Good);
        p.State = FlashcardPromptState.Active;
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
    /// Add prompts to a card. Validates each pair and refuses a duplicate of a question already on the
    /// card. With <paramref name="backfill"/> the card's own date is the exposure and first reviews are
    /// spread (see class docs). Adds to the context but does not save. Returns the new rows, or an error.
    /// </summary>
    public static async Task<(List<FlashcardPrompt> Created, string? Error)> CreatePromptsAsync(
        RoadmapDbContext db, Flashcard card, IReadOnlyList<FlashcardPromptInput>? inputs, bool backfill)
    {
        if (inputs is null || inputs.Count == 0) return ([], "prompts is required — at least one { question, answer }");

        var existing = await db.FlashcardPrompts.Where(p => p.FlashcardId == card.Id).ToListAsync();
        if (existing.Count + inputs.Count > MaxPromptsPerCard)
            return ([], $"a card holds at most {MaxPromptsPerCard} prompts; this one has {existing.Count} and {inputs.Count} more were given");

        var seen = new HashSet<string>(existing.Select(p => p.Question.Trim().ToLowerInvariant()));
        var nowUtc = DateTime.UtcNow;
        var today = AppClock.Today();
        var sort = existing.Count == 0 ? 0 : existing.Max(p => p.SortOrder) + 1;

        var firstInterval = Fsrs.IntervalDays(Fsrs.Initial(FsrsGrade.Good).Stability, DesiredRetention);

        var created = new List<FlashcardPrompt>();
        foreach (var input in inputs)
        {
            var err = ValidatePrompt(input.Question, input.Answer);
            if (err is not null) return ([], err);
            var key = input.Question.Trim().ToLowerInvariant();
            if (!seen.Add(key)) return ([], $"duplicate question: \"{input.Question.Trim()}\"");
            var p = NewPrompt(card, input.Question, input.Answer, sort++, nowUtc, today);
            if (backfill && card.EntryDate < today)
            {
                // Written on the card's date and never answered since: due when it would have been.
                p.LastReviewedAt = AppClock.StartOfDayUtc(card.EntryDate);
                var due = card.EntryDate.AddDays(firstInterval);
                p.DueOn = due < today ? today : due;
            }
            p.Flashcard = card;
            db.FlashcardPrompts.Add(p);
            created.Add(p);
        }
        return (created, null);
    }

    /// <summary>PATCH a prompt: null keeps a field. <paramref name="reset"/> restarts the schedule. Returns an error or null.</summary>
    public static string? ApplyUpdate(FlashcardPrompt p, string? question, string? answer, string? state, bool reset)
    {
        var q = question is null ? p.Question : question.Trim();
        var a = answer is null ? p.Answer : answer.Trim();
        var err = ValidatePrompt(q, a);
        if (err is not null) return err;

        FlashcardPromptState? newState = null;
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

    /// <summary>
    /// Apply a graded review: move the FSRS state, stamp the next due day and build the immutable
    /// review row. Not attached and not saved — the caller does <c>db.FlashcardReviews.Add(review)</c>
    /// (adding through the navigation would make EF treat the pre-keyed row as Modified).
    /// A lapse comes back tomorrow flagged relearning; the eighth lapse suspends the prompt. A pass is
    /// due again when predicted recall will have fallen to <see cref="DesiredRetention"/>.
    /// </summary>
    public static FlashcardReview ApplyReview(FlashcardPrompt p, FsrsGrade grade, string? answer, string? note, DateOnly today, DateTime nowUtc)
    {
        var elapsed = ElapsedDays(p, today);
        var r = Fsrs.Retrievability(p.Stability, elapsed);
        var before = new Fsrs.State(p.Difficulty, p.Stability);
        var after = Fsrs.Next(before, r, grade);

        var review = new FlashcardReview
        {
            Id = Guid.NewGuid(),
            FlashcardPromptId = p.Id,
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
        if (p.State == FlashcardPromptState.Parked) p.State = FlashcardPromptState.Active;

        if (grade == FsrsGrade.Again)
        {
            p.Lapses++;
            p.Relearning = true;
            p.DueOn = today.AddDays(1);
            if (p.Lapses >= LeechLapses) p.State = FlashcardPromptState.Suspended;
        }
        else
        {
            p.Relearning = false;
            p.DueOn = today.AddDays(Fsrs.IntervalDays(after.Stability, DesiredRetention));
        }
        return review;
    }

    // ===== Queries =====

    /// <summary>Reviews recorded today for prompts of one book — what that book's cap counts.</summary>
    public static Task<int> AskedTodayAsync(RoadmapDbContext db, DateOnly today, string book) =>
        db.FlashcardReviews.CountAsync(r => r.ReviewDate == today && r.FlashcardPrompt!.Flashcard!.Book == book);

    /// <summary>
    /// Today's queue under the cap. <paramref name="persist"/> false (the tab's read-only preview) computes
    /// the same queue without parking or unparking anything; a real session passes true.
    /// </summary>
    public static async Task<FlashcardSessionDto> BuildSessionAsync(RoadmapDbContext db, string book, bool persist)
    {
        var today = AppClock.Today();
        var nowUtc = DateTime.UtcNow;
        var askedToday = await AskedTodayAsync(db, today, book);
        var remaining = Math.Max(0, DailyCap - askedToday);

        IQueryable<FlashcardPrompt> q = db.FlashcardPrompts.Include(p => p.Flashcard)
            .Where(p => (p.State == FlashcardPromptState.Active && p.DueOn <= today) || p.State == FlashcardPromptState.Parked);
        q = q.Where(p => p.Flashcard!.Book == book);
        if (!persist) q = q.AsNoTracking();
        var candidates = await q.ToListAsync();

        var due = candidates.Where(p => p.State == FlashcardPromptState.Active).ToList();
        var parked = candidates.Where(p => p.State == FlashcardPromptState.Parked).ToList();

        var queue = due.Where(p => p.Relearning).OrderBy(p => p.DueOn).ThenBy(p => p.CreatedAt)
            .Concat(due.Where(p => !p.Relearning)
                .OrderByDescending(p => Retrievability(p, today)).ThenBy(p => p.DueOn).ThenBy(p => p.CreatedAt))
            .ToList();

        var taken = queue.Take(remaining).ToList();
        var overflow = queue.Skip(remaining).ToList();

        var parkedNow = 0;
        // Only reviewed prompts are parked; a never-answered backlog stays due so its size stays visible.
        foreach (var p in overflow.Where(p => !p.Relearning && p.Reviews > 0 && Retrievability(p, today) < ParkBelow))
        {
            if (persist) { p.State = FlashcardPromptState.Parked; p.UpdatedAt = nowUtc; }
            parkedNow++;
        }

        var unparked = new List<FlashcardPrompt>();
        var spare = remaining - taken.Count;
        if (spare > 0)
        {
            foreach (var p in parked.OrderByDescending(p => Retrievability(p, today)).ThenBy(p => p.DueOn).Take(spare))
            {
                if (persist) { p.State = FlashcardPromptState.Active; p.UpdatedAt = nowUtc; }
                unparked.Add(p);
            }
        }

        if (persist) await db.SaveChangesAsync();

        var prompts = taken.Concat(unparked).Select(p => ToPromptDto(p, today, includeHistory: false)).ToList();
        return new FlashcardSessionDto(
            Date: today.ToString("yyyy-MM-dd"), DailyCap: DailyCap, AskedToday: askedToday, Remaining: remaining,
            DueTotal: due.Count, Returned: prompts.Count, Overflow: overflow.Count, ParkedNow: parkedNow,
            Unparked: unparked.Count, ParkedTotal: parked.Count - unparked.Count + parkedNow,
            CarryCapacityPerDay: CarryCapacityPerDay, DesiredRetention: DesiredRetention, Prompts: prompts);
    }

    public static async Task<FlashcardStatsDto> StatsAsync(RoadmapDbContext db, string book)
    {
        var today = AppClock.Today();
        var prompts = await db.FlashcardPrompts.AsNoTracking().Where(p => p.Flashcard!.Book == book)
            .Select(p => new { p.FlashcardId, p.State, p.DueOn, p.Stability, p.Lapses })
            .ToListAsync();
        var cardCount = await db.Flashcards.CountAsync(c => c.Book == book);
        var cardsWith = prompts.Select(p => p.FlashcardId).Distinct().Count();
        var askedToday = await AskedTodayAsync(db, today, book);

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var monthAgo = today.AddDays(-30);
        var bookReviews = db.FlashcardReviews.Where(r => r.FlashcardPrompt!.Flashcard!.Book == book);
        var reviewsAll = await bookReviews.CountAsync();
        var reviews7 = await bookReviews.CountAsync(r => r.ReviewedAt >= weekAgo);
        var recent = await bookReviews.AsNoTracking()
            .Where(r => r.ReviewDate >= monthAgo && !r.WasRelearning)
            .Select(r => r.Grade).ToListAsync();
        double? trueRetention = recent.Count == 0 ? null : Math.Round(recent.Count(g => g > (int)FsrsGrade.Again) / (double)recent.Count, 3);

        var active = prompts.Where(p => p.State == FlashcardPromptState.Active).ToList();
        var dueToday = active.Count(p => p.DueOn <= today);
        var load = Enumerable.Range(0, 7).Select(i =>
        {
            var d = today.AddDays(i);
            return new DayLoadDto(d.ToString("yyyy-MM-dd"), i == 0 ? dueToday : active.Count(p => p.DueOn == d));
        }).ToList();

        return new FlashcardStatsDto(
            Flashcards: cardCount,
            FlashcardsWithPrompts: cardsWith,
            FlashcardsWithoutPrompts: Math.Max(0, cardCount - cardsWith),
            Prompts: prompts.Count,
            Active: active.Count,
            Parked: prompts.Count(p => p.State == FlashcardPromptState.Parked),
            Suspended: prompts.Count(p => p.State == FlashcardPromptState.Suspended),
            Leeches: prompts.Count(p => p.State == FlashcardPromptState.Suspended && p.Lapses >= LeechLapses),
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

    /// <summary>
    /// Cards newest first (by date, then creation), filtered by book, an exact date or a date range,
    /// a text search, and "no prompts yet". Counts only — no prompt bodies. Returns (total matching, page).
    /// </summary>
    public static async Task<(int Total, List<FlashcardDto> Cards)> ListCardsAsync(RoadmapDbContext db, string book,
        DateOnly? date, DateOnly? from, DateOnly? to, string? search, bool withoutPromptsOnly, int limit)
    {
        var today = AppClock.Today();
        var q = db.Flashcards.AsNoTracking().Where(c => c.Book == book);
        if (date is { } d) q = q.Where(c => c.EntryDate == d);
        if (from is { } f) q = q.Where(c => c.EntryDate >= f);
        if (to is { } t) q = q.Where(c => c.EntryDate <= t);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(c => EF.Functions.ILike(c.Content, pattern) || c.Prompts.Any(p => EF.Functions.ILike(p.Question, pattern)));
        }
        if (withoutPromptsOnly) q = q.Where(c => !c.Prompts.Any());
        var total = await q.CountAsync();
        var cards = await q.OrderByDescending(c => c.EntryDate).ThenBy(c => c.CreatedAt).Take(limit).ToListAsync();
        var ids = cards.Select(c => c.Id).ToList();
        var counts = await db.FlashcardPrompts.AsNoTracking().Where(p => ids.Contains(p.FlashcardId))
            .Select(p => new { p.FlashcardId, p.State, p.DueOn }).ToListAsync();
        var byCard = counts.ToLookup(x => x.FlashcardId);
        return (total, cards.Select(c =>
        {
            var ps = byCard[c.Id].ToList();
            return new FlashcardDto(c.Id, c.Book, c.EntryDate.ToString("yyyy-MM-dd"), c.Content,
                ps.Count,
                ps.Count(x => x.State == FlashcardPromptState.Active && x.DueOn <= today),
                ps.Count(x => x.State == FlashcardPromptState.Parked),
                ps.Count(x => x.State == FlashcardPromptState.Suspended),
                ps.Where(x => x.State == FlashcardPromptState.Active).Select(x => (DateOnly?)x.DueOn).Min()?.ToString("yyyy-MM-dd"),
                c.CreatedAt, c.UpdatedAt, []);
        }).ToList());
    }

    /// <summary>
    /// Every prompt's stability and days since it was last seen — the inputs of the forgetting curve —
    /// for the dashboard, which projects recall forward on the client. Suspended prompts are included and
    /// flagged by state; the tab decides what to show.
    /// </summary>
    public static async Task<FlashcardMemoryDto> MemoryAsync(RoadmapDbContext db, string book)
    {
        var today = AppClock.Today();
        var q = db.FlashcardPrompts.AsNoTracking().Include(p => p.Flashcard).Where(p => p.Flashcard!.Book == book);
        var rows = await q.ToListAsync();
        var ids = rows.Select(p => p.Id).ToList();
        var reviews = (await db.FlashcardReviews.AsNoTracking().Where(r => ids.Contains(r.FlashcardPromptId))
                .OrderBy(r => r.ReviewedAt).ToListAsync())
            .ToLookup(r => r.FlashcardPromptId);
        return new FlashcardMemoryDto(today.ToString("yyyy-MM-dd"), DesiredRetention, rows.Select(p =>
        {
            var history = reviews[p.Id].ToList();
            // The first exposure: before any review it is LastReviewedAt itself; once reviewed, the first review
            // records how long after the exposure it came and the stability it started from.
            var first = history.FirstOrDefault();
            var exposure = first is null ? LastReviewDate(p) : first.ReviewDate.AddDays(-first.ElapsedDays);
            var initial = first is null ? p.Stability : first.StabilityBefore;
            return new FlashcardMemoryPointDto(p.Flashcard?.Book ?? "", p.State.ToString(),
                Math.Round(p.Stability, 3), ElapsedDays(p, today),
                exposure.ToString("yyyy-MM-dd"), Math.Round(initial, 3),
                history.Select(r => new FlashcardMemoryReviewDto(r.ReviewDate.ToString("yyyy-MM-dd"), Math.Round(r.StabilityAfter, 3))).ToList());
        }).ToList());
    }

    // ===== DTOs =====

    public static FlashcardDto ToCardDto(Flashcard c, DateOnly today, bool includeHistory)
    {
        var ps = c.Prompts.OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToList();
        foreach (var p in ps) p.Flashcard ??= c;
        return new FlashcardDto(c.Id, c.Book, c.EntryDate.ToString("yyyy-MM-dd"), c.Content,
            ps.Count,
            ps.Count(p => IsDue(p, today)),
            ps.Count(p => p.State == FlashcardPromptState.Parked),
            ps.Count(p => p.State == FlashcardPromptState.Suspended),
            ps.Where(p => p.State == FlashcardPromptState.Active).Select(p => (DateOnly?)p.DueOn).Min()?.ToString("yyyy-MM-dd"),
            c.CreatedAt, c.UpdatedAt,
            ps.Select(p => ToPromptDto(p, today, includeHistory)).ToList());
    }

    public static FlashcardPromptDto ToPromptDto(FlashcardPrompt p, DateOnly today, bool includeHistory) => new(
        p.Id, p.FlashcardId,
        p.Flashcard?.Book ?? "", p.Flashcard?.EntryDate.ToString("yyyy-MM-dd") ?? "",
        p.Question, p.Answer, p.SortOrder, p.State.ToString(),
        Math.Round(p.Difficulty, 2), Math.Round(p.Stability, 1), Math.Round(Retrievability(p, today), 3),
        p.DueOn.ToString("yyyy-MM-dd"), IsDue(p, today), p.Relearning, p.Lapses, p.Reviews, p.LastReviewedAt, p.CreatedAt,
        includeHistory
            ? p.ReviewHistory.OrderByDescending(r => r.ReviewedAt).Select(r => new FlashcardPromptReviewDto(
                r.ReviewedAt, ((FsrsGrade)r.Grade).ToString(), r.ElapsedDays, Math.Round(r.Retrievability, 3),
                Math.Round(r.StabilityBefore, 1), Math.Round(r.StabilityAfter, 1),
                Math.Round(r.DifficultyBefore, 2), Math.Round(r.DifficultyAfter, 2),
                r.WasRelearning, r.Answer, r.Note)).ToList()
            : []);
}
