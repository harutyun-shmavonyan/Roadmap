using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

/// <summary>
/// Notes v2 — flashcards with FSRS-scheduled prompts, a system of its own beside the v1 daily notes.
/// The note-taking skill writes a card (<c>create_flashcard</c>) with its prompts; the quiz pulls what
/// is due under the daily cap (<c>get_due_flashcard_prompts</c>) and records how each went
/// (<c>record_flashcard_review</c>). Scheduling is deliberately NOT exposed: the caller supplies a grade
/// and the server decides when the prompt comes back (see <see cref="Fsrs"/>).
/// </summary>
[McpServerToolType]
public sealed class FlashcardMcpTools(RoadmapDbContext db)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    private static object CardRef(Flashcard c) => new { flashcard_id = c.Id, book = c.Book, day_number = c.DayNumber, entry_date = c.EntryDate.ToString("yyyy-MM-dd") };

    private async Task<Flashcard?> FindCardAsync(string book, int number, bool tracking) =>
        tracking
            ? await db.Flashcards.Include(c => c.Prompts).FirstOrDefaultAsync(c => c.Book == book && c.DayNumber == number)
            : await db.Flashcards.AsNoTracking().Include(c => c.Prompts).FirstOrDefaultAsync(c => c.Book == book && c.DayNumber == number);

    // ===== Cards =====

    [McpServerTool(Name = "create_flashcard"), Description(
        "Notes v2: write a day's learning as a flashcard in a book ('red' = professional/technical, 'green' = general) and attach its " +
        "prompts in the same call. One card per book per day: if a card already exists for that date the content is appended to it " +
        "and the prompts are added. Content is the day's bullets (Markdown). Prompts are atomic question/answer pairs — as many as " +
        "the subpoints need, one fact each, the question carrying no hint of the answer. Writing the card counts as the first exposure, " +
        "so each prompt is already scheduled: first review in about 4 days, nothing asked today. For a card older than today pass " +
        "backfill=true (default when the date is in the past): the card's date counts as the exposure and first reviews are spread at " +
        "10 a day so a bulk import never floods one date. Returns { flashcard_id, day_number, action, prompts_created, first_review_in_days }.")]
    public async Task<string> CreateFlashcard(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The day's content, Markdown bullets")] string content,
        [Description("Date (YYYY-MM-DD), defaults to today in Asia/Yerevan")] string? date = null,
        [Description("Prompts to attach: each { question, answer }")] FlashcardPromptInput[]? prompts = null,
        [Description("Backfill mode (see above). Defaults to true when the date is before today, else false.")] bool? backfill = null)
    {
        if (!FlashcardLogic.TryBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        if (string.IsNullOrWhiteSpace(content) && (prompts is null || prompts.Length == 0)) return J(new { error = "content is required" });
        DateOnly entryDate;
        if (date is null) entryDate = AppClock.Today();
        else if (!DateOnly.TryParse(date, out entryDate)) return J(new { error = "Invalid date. Use YYYY-MM-DD." });

        var (card, action) = await FlashcardLogic.UpsertCardAsync(db, bk, content ?? "", entryDate);
        var created = new List<FlashcardPrompt>();
        if (prompts is { Length: > 0 })
        {
            var (list, error) = await FlashcardLogic.CreatePromptsAsync(db, card, prompts, backfill ?? entryDate < AppClock.Today());
            if (error is not null) return J(new { error, card = CardRef(card), action, note = "the card was saved; the prompts were not" });
            await db.SaveChangesAsync();
            created = list;
        }
        var today = AppClock.Today();
        var total = await db.FlashcardPrompts.CountAsync(p => p.FlashcardId == card.Id);
        return J(new
        {
            status = action,
            card = CardRef(card),
            prompts_created = created.Count,
            total_prompts_on_card = total,
            first_review_in_days = created.Count == 0 ? (int?)null : created.Min(p => p.DueOn.DayNumber) - today.DayNumber,
            created = created.Select(p => new { prompt_id = p.Id, p.Question, due_on = p.DueOn.ToString("yyyy-MM-dd") }),
        });
    }

    [McpServerTool(Name = "get_flashcard"), Description("Notes v2: one card by book and day_number, with its content and all its prompts (state, stability, predicted recall, next due).")]
    public async Task<string> GetFlashcard(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The card's day_number")] int number,
        [Description("Include every recorded review of each prompt (default false)")] bool include_history = false)
    {
        if (!FlashcardLogic.TryBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        IQueryable<Flashcard> q = db.Flashcards.AsNoTracking().Include(c => c.Prompts);
        if (include_history) q = q.Include(c => c.Prompts).ThenInclude(p => p.ReviewHistory);
        var card = await q.FirstOrDefaultAsync(c => c.Book == bk && c.DayNumber == number);
        return card is null ? J(new { error = "Flashcard not found" }) : J(FlashcardLogic.ToCardDto(card, AppClock.Today(), include_history));
    }

    [McpServerTool(Name = "list_flashcards"), Description(
        "Notes v2: cards newest first, with content and prompt counts (no prompt bodies). without_prompts_only=true is the backfill " +
        "list — cards that still need prompts; give each its prompts with add_flashcard_prompts (backfill=true).")]
    public async Task<string> ListFlashcards(
        [Description("Restrict to one book: 'red' or 'green'. Omit for both.")] string? book = null,
        [Description("Max cards (default 20, max 200)")] int limit = 20,
        [Description("Only cards with no prompts yet (default false)")] bool without_prompts_only = false)
    {
        string? bk = null;
        if (!string.IsNullOrWhiteSpace(book)) { if (!FlashcardLogic.TryBook(book, out bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." }); }
        if (limit is < 1 or > 200) limit = 20;
        var cards = await FlashcardLogic.ListCardsAsync(db, bk, limit, without_prompts_only);
        var total = without_prompts_only
            ? await db.Flashcards.CountAsync(c => (bk == null || c.Book == bk) && !c.Prompts.Any())
            : await db.Flashcards.CountAsync(c => bk == null || c.Book == bk);
        return J(new { total, returned = cards.Count, cards });
    }

    [McpServerTool(Name = "update_flashcard"), Description("Notes v2: replace a card's content (overwrites, does not append). Prompts are untouched.")]
    public async Task<string> UpdateFlashcard(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The card's day_number")] int number,
        [Description("The new full content")] string content)
    {
        if (!FlashcardLogic.TryBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        var card = await FindCardAsync(bk, number, tracking: true);
        if (card is null) return J(new { error = "Flashcard not found" });
        card.Content = content ?? "";
        card.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(FlashcardLogic.ToCardDto(card, AppClock.Today(), includeHistory: false));
    }

    [McpServerTool(Name = "delete_flashcard"), Description("Notes v2: delete a card with all its prompts and their review history. Leaves a gap in the day_number sequence.")]
    public async Task<string> DeleteFlashcard(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The card's day_number")] int number)
    {
        if (!FlashcardLogic.TryBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        var card = await db.Flashcards.FirstOrDefaultAsync(c => c.Book == bk && c.DayNumber == number);
        if (card is null) return J(new { error = "Flashcard not found" });
        db.Flashcards.Remove(card);
        await db.SaveChangesAsync();
        return J(new { deleted = true, book = bk, day_number = number });
    }

    // ===== Prompts =====

    [McpServerTool(Name = "add_flashcard_prompts"), Description(
        "Notes v2: attach more prompts to an existing card — as many as its subpoints need, one fact each. Duplicates of a question " +
        "already on the card are refused. backfill=true (default when the card is older than today) dates the exposure to the card and " +
        "spreads first reviews at 10 a day.")]
    public async Task<string> AddFlashcardPrompts(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The card's day_number")] int number,
        [Description("The prompts: each { question, answer }")] FlashcardPromptInput[] prompts,
        [Description("Backfill mode; defaults to true when the card's date is before today")] bool? backfill = null)
    {
        if (!FlashcardLogic.TryBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        var card = await db.Flashcards.FirstOrDefaultAsync(c => c.Book == bk && c.DayNumber == number);
        if (card is null) return J(new { error = "Flashcard not found" });
        var (created, error) = await FlashcardLogic.CreatePromptsAsync(db, card, prompts, backfill ?? card.EntryDate < AppClock.Today());
        if (error is not null) return J(new { error });
        await db.SaveChangesAsync();
        var today = AppClock.Today();
        return J(new
        {
            status = "created",
            card = CardRef(card),
            prompts_created = created.Count,
            total_prompts_on_card = await db.FlashcardPrompts.CountAsync(p => p.FlashcardId == card.Id),
            first_review_in_days = created.Count == 0 ? (int?)null : created.Min(p => p.DueOn.DayNumber) - today.DayNumber,
            created = created.Select(p => new { prompt_id = p.Id, p.Question, due_on = p.DueOn.ToString("yyyy-MM-dd") }),
        });
    }

    [McpServerTool(Name = "update_flashcard_prompt"), Description(
        "Notes v2: edit a prompt. Only the fields you pass change. state: Active | Parked | Suspended. reset=true restarts the schedule as " +
        "if just written — use it with a rewritten question/answer after a leech was suspended, so the fixed prompt starts fresh.")]
    public async Task<string> UpdateFlashcardPrompt(
        [Description("Prompt UUID")] Guid prompt_id,
        [Description("New question")] string? question = null,
        [Description("New answer")] string? answer = null,
        [Description("Active | Parked | Suspended")] string? state = null,
        [Description("Restart the schedule from scratch (default false)")] bool reset = false)
    {
        var p = await db.FlashcardPrompts.Include(x => x.Flashcard).FirstOrDefaultAsync(x => x.Id == prompt_id);
        if (p is null) return J(new { error = "prompt not found" });
        var error = FlashcardLogic.ApplyUpdate(p, question, answer, state, reset);
        if (error is not null) return J(new { error });
        await db.SaveChangesAsync();
        return J(new { status = "updated", prompt = FlashcardLogic.ToPromptDto(p, AppClock.Today(), includeHistory: false) });
    }

    [McpServerTool(Name = "delete_flashcard_prompt"), Description("Notes v2: permanently delete a prompt and its review history. The card is untouched.")]
    public async Task<string> DeleteFlashcardPrompt([Description("Prompt UUID")] Guid prompt_id)
    {
        var p = await db.FlashcardPrompts.FindAsync(prompt_id);
        if (p is null) return J(new { error = "prompt not found" });
        db.FlashcardPrompts.Remove(p);
        await db.SaveChangesAsync();
        return J(new { status = "deleted", prompt_id, question = p.Question });
    }

    // ===== The daily session =====

    [McpServerTool(Name = "get_due_flashcard_prompts"), Description(
        "Notes v2: today's review queue, already triaged under the daily cap of 25 questions (remaining = cap minus what was already " +
        "recorded today, across both books). Ask the prompts IN THE ORDER RETURNED: yesterday's lapses lead, then due prompts by " +
        "predicted recall, highest first. Each prompt carries its answer for grading — never show it before the learner answers. Record " +
        "every answer with record_flashcard_review; a prompt you skip stays due. Calling this again the same day returns what is still due " +
        "within the remaining budget, so it is safe to resume. Side effects: due prompts that did not fit and have fallen below 50% " +
        "predicted recall are parked (not deleted); on a day with spare slots parked prompts are pulled back in.")]
    public async Task<string> GetDueFlashcardPrompts(
        [Description("Restrict to one book: 'red' or 'green'. Omit for both (the default — the cap is shared anyway).")] string? book = null)
    {
        string? bk = null;
        if (!string.IsNullOrWhiteSpace(book)) { if (!FlashcardLogic.TryBook(book, out bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." }); }
        return J(await FlashcardLogic.BuildSessionAsync(db, bk, persist: true));
    }

    [McpServerTool(Name = "record_flashcard_review"), Description(
        "Notes v2: record how one prompt went and let the server reschedule it (FSRS). grade: 'again' = forgot or wrong (back tomorrow, " +
        "stability collapses); 'hard' = right in part or with real effort; 'good' = recalled; 'easy' = instant and complete, or the learner " +
        "answered 'O' meaning they know it perfectly (pass answer='O'). Returns the new stability, next due date, how many questions remain " +
        "under today's cap, and leech=true when this lapse suspended the prompt (offer to rewrite it with update_flashcard_prompt reset=true).")]
    public async Task<string> RecordFlashcardReview(
        [Description("Prompt UUID (from get_due_flashcard_prompts)")] Guid prompt_id,
        [Description("again | hard | good | easy")] string grade,
        [Description("What the learner actually answered ('O' when they declared it known)")] string? answer = null,
        [Description("Why this grade — one line")] string? note = null)
    {
        if (!FlashcardLogic.TryParseGrade(grade, out var g)) return J(new { error = $"invalid grade '{grade}'. Use again | hard | good | easy." });
        var p = await db.FlashcardPrompts.Include(x => x.Flashcard).FirstOrDefaultAsync(x => x.Id == prompt_id);
        if (p is null) return J(new { error = "prompt not found" });

        var today = AppClock.Today();
        var review = FlashcardLogic.ApplyReview(p, g, answer, note, today, DateTime.UtcNow);
        db.FlashcardReviews.Add(review);
        await db.SaveChangesAsync();

        var askedToday = await FlashcardLogic.AskedTodayAsync(db, today);
        return J(new
        {
            status = g == FsrsGrade.Again ? "failed" : "passed",
            grade = g.ToString(),
            question = p.Question,
            card = p.Flashcard is null ? null : CardRef(p.Flashcard),
            retrievability_before = Math.Round(review.Retrievability, 3),
            elapsed_days = review.ElapsedDays,
            stability_before_days = Math.Round(review.StabilityBefore, 1),
            stability_days = Math.Round(p.Stability, 1),
            difficulty = Math.Round(p.Difficulty, 2),
            due_on = p.DueOn.ToString("yyyy-MM-dd"),
            interval_days = p.DueOn.DayNumber - today.DayNumber,
            relearning = p.Relearning,
            lapses = p.Lapses,
            reviews = p.Reviews,
            state = p.State.ToString(),
            leech = FlashcardLogic.IsLeech(p),
            asked_today = askedToday,
            remaining_today = Math.Max(0, FlashcardLogic.DailyCap - askedToday),
            daily_cap = FlashcardLogic.DailyCap,
        });
    }

    [McpServerTool(Name = "get_flashcard_stats"), Description(
        "Notes v2 totals: cards with and without prompts, prompts by state, due today, asked today vs the daily cap, carry capacity, " +
        "true retention over 30 days (target 0.9), lapses, and the due count for each of the next 7 days.")]
    public async Task<string> GetFlashcardStats() => J(await FlashcardLogic.StatsAsync(db));
}
