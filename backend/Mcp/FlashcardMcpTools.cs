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

    private static object CardRef(Flashcard c) => new { flashcard_id = c.Id, book = c.Book, entry_date = c.EntryDate.ToString("yyyy-MM-dd") };

    private static bool TryDate(string? raw, out DateOnly? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (!DateOnly.TryParse(raw, out var d)) return false;
        value = d;
        return true;
    }

    // ===== Cards =====

    [McpServerTool(Name = "create_flashcard"), Description(
        "Notes v2: save ONE note as a new flashcard in a book ('red' = professional/technical, 'green' = general) and attach its " +
        "prompts in the same call. One note = one card: every call creates a new card, even on a day that already has cards (the date " +
        "is just a property of the card). Content is the note — one top-level bullet with its subpoints (Markdown). Prompts are atomic " +
        "question/answer pairs — as many as the subpoints need, one fact each, the question carrying no hint of the answer. Writing the " +
        "card counts as the first exposure, so each prompt is already scheduled: first review in about 4 days, nothing asked today. For " +
        "a card dated before today, backfill defaults to true: the card's date counts as the exposure, so its prompts are due when they " +
        "would have been (card date + 4 days — today for anything older); the daily cap paces a backlog. Returns { flashcard_id, prompts_created, first_review_in_days }.")]
    public async Task<string> CreateFlashcard(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The note: one top-level bullet and its subpoints, Markdown")] string content,
        [Description("Date the note was learned (YYYY-MM-DD), defaults to today in Asia/Yerevan")] string? date = null,
        [Description("Prompts to attach: each { question, answer }")] FlashcardPromptInput[]? prompts = null,
        [Description("Backfill mode (see above). Defaults to true when the date is before today, else false.")] bool? backfill = null)
    {
        if (!FlashcardLogic.TryBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        if (string.IsNullOrWhiteSpace(content)) return J(new { error = "content is required" });
        if (!TryDate(date, out var d)) return J(new { error = "Invalid date. Use YYYY-MM-DD." });
        var entryDate = d ?? AppClock.Today();

        var card = await FlashcardLogic.CreateCardAsync(db, bk, content, entryDate);
        var created = new List<FlashcardPrompt>();
        if (prompts is { Length: > 0 })
        {
            var (list, error) = await FlashcardLogic.CreatePromptsAsync(db, card, prompts, backfill ?? entryDate < AppClock.Today());
            if (error is not null) return J(new { error, card = CardRef(card), note = "the card was saved; the prompts were not — fix and call add_flashcard_prompts" });
            await db.SaveChangesAsync();
            created = list;
        }
        var today = AppClock.Today();
        return J(new
        {
            status = "created",
            card = CardRef(card),
            prompts_created = created.Count,
            first_review_in_days = created.Count == 0 ? (int?)null : created.Min(p => p.DueOn.DayNumber) - today.DayNumber,
            created = created.Select(p => new { prompt_id = p.Id, p.Question, due_on = p.DueOn.ToString("yyyy-MM-dd") }),
        });
    }

    [McpServerTool(Name = "get_flashcard"), Description("Notes v2: one card by id, with its content and all its prompts (state, stability, predicted recall, next due).")]
    public async Task<string> GetFlashcard(
        [Description("Flashcard UUID")] Guid flashcard_id,
        [Description("Include every recorded review of each prompt (default false)")] bool include_history = false)
    {
        IQueryable<Flashcard> q = db.Flashcards.AsNoTracking().Include(c => c.Prompts);
        if (include_history) q = q.Include(c => c.Prompts).ThenInclude(p => p.ReviewHistory);
        var card = await q.FirstOrDefaultAsync(c => c.Id == flashcard_id);
        return card is null ? J(new { error = "Flashcard not found" }) : J(FlashcardLogic.ToCardDto(card, AppClock.Today(), include_history));
    }

    [McpServerTool(Name = "list_flashcards"), Description(
        "Notes v2: one book's cards newest first (by date), with content and prompt counts (no prompt bodies). Filter by an exact date " +
        "(all the cards learned that day), by a from/to date range, by text (matches content or a prompt question), or " +
        "without_prompts_only=true for the backfill list — cards that still need prompts (give them with add_flashcard_prompts).")]
    public async Task<string> ListFlashcards(
        [Description("Book: 'red' or 'green' — the two books are separate; there is no combined listing")] string book,
        [Description("Only cards of this date (YYYY-MM-DD)")] string? date = null,
        [Description("Only cards on or after this date (YYYY-MM-DD)")] string? from_date = null,
        [Description("Only cards on or before this date (YYYY-MM-DD)")] string? to_date = null,
        [Description("Text to look for in the card content or its prompt questions")] string? search = null,
        [Description("Only cards with no prompts yet (default false)")] bool without_prompts_only = false,
        [Description("Max cards (default 20, max 200)")] int limit = 20)
    {
        if (!FlashcardLogic.TryBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        if (!TryDate(date, out var d) || !TryDate(from_date, out var f) || !TryDate(to_date, out var t)) return J(new { error = "Invalid date. Use YYYY-MM-DD." });
        if (limit is < 1 or > 200) limit = 20;
        var (total, cards) = await FlashcardLogic.ListCardsAsync(db, bk, d, f, t, search, without_prompts_only, limit);
        return J(new { total, returned = cards.Count, cards });
    }

    [McpServerTool(Name = "update_flashcard"), Description("Notes v2: replace a card's content (overwrites). Prompts are untouched.")]
    public async Task<string> UpdateFlashcard(
        [Description("Flashcard UUID")] Guid flashcard_id,
        [Description("The new content")] string content)
    {
        var card = await db.Flashcards.Include(c => c.Prompts).FirstOrDefaultAsync(c => c.Id == flashcard_id);
        if (card is null) return J(new { error = "Flashcard not found" });
        card.Content = content ?? "";
        card.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(FlashcardLogic.ToCardDto(card, AppClock.Today(), includeHistory: false));
    }

    [McpServerTool(Name = "delete_flashcard"), Description("Notes v2: delete a card with all its prompts and their review history.")]
    public async Task<string> DeleteFlashcard([Description("Flashcard UUID")] Guid flashcard_id)
    {
        var card = await db.Flashcards.FirstOrDefaultAsync(c => c.Id == flashcard_id);
        if (card is null) return J(new { error = "Flashcard not found" });
        db.Flashcards.Remove(card);
        await db.SaveChangesAsync();
        return J(new { deleted = true, flashcard_id });
    }

    [McpServerTool(Name = "split_flashcard"), Description(
        "Notes v2: split one card into several — e.g. a card that holds more than one note. Each part is { content, prompt_ids }. Every " +
        "prompt of the card must be assigned to exactly one part; prompts move with their schedule and full review history. The new " +
        "cards keep the book and date, in the order given; the original card is deleted. Returns the new cards.")]
    public async Task<string> SplitFlashcard(
        [Description("Flashcard UUID")] Guid flashcard_id,
        [Description("The parts, in order: each { content, prompt_ids }")] FlashcardSplitInput[] parts)
    {
        var mapped = parts?.Select(p => new FlashcardSplitPart(p.content, p.prompt_ids?.ToList())).ToList();
        var (cards, error) = await FlashcardLogic.SplitCardAsync(db, flashcard_id, mapped);
        if (error is not null) return J(new { error });
        return J(new
        {
            status = "split",
            from = flashcard_id,
            cards = cards.Select(c => new { flashcard_id = c.Id, prompts = c.Prompts.Count, first_line = c.Content.Split('\n')[0] }),
        });
    }

    // ===== Prompts =====

    [McpServerTool(Name = "add_flashcard_prompts"), Description(
        "Notes v2: attach more prompts to an existing card — as many as its subpoints need, one fact each. Duplicates of a question " +
        "already on the card are refused. backfill defaults to true when the card is dated before today (exposure dated to the card, " +
        "due at card date + 4 days, i.e. today for anything older).")]
    public async Task<string> AddFlashcardPrompts(
        [Description("Flashcard UUID")] Guid flashcard_id,
        [Description("The prompts: each { question, answer }")] FlashcardPromptInput[] prompts,
        [Description("Backfill mode; defaults to true when the card's date is before today")] bool? backfill = null)
    {
        var card = await db.Flashcards.FirstOrDefaultAsync(c => c.Id == flashcard_id);
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
        "Notes v2: today's review queue for ONE book, already triaged under that book's daily cap of 25 questions (remaining = cap minus " +
        "what was already recorded today in that book; red and green each have their own cap). Ask the prompts IN THE ORDER RETURNED: yesterday's lapses lead, then due prompts by " +
        "predicted recall, highest first. Each prompt carries its answer for grading — never show it before the learner answers. Record " +
        "every answer with record_flashcard_review; a prompt you skip stays due. Calling this again the same day returns what is still due " +
        "within the remaining budget, so it is safe to resume. Side effects: due prompts that did not fit and have fallen below 50% " +
        "predicted recall and were answered before are parked (not deleted); never-answered prompts stay due. On a day with spare slots " +
        "parked prompts are pulled back in.")]
    public async Task<string> GetDueFlashcardPrompts(
        [Description("Book: 'red' or 'green'")] string book)
    {
        if (!FlashcardLogic.TryBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
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

        var askedToday = await FlashcardLogic.AskedTodayAsync(db, today, p.Flashcard!.Book);
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
        "Notes v2 totals for ONE book: cards with and without prompts, prompts by state, due today, asked today vs the daily cap, carry capacity, " +
        "true retention over 30 days (target 0.9), lapses, and the due count for each of the next 7 days.")]
    public async Task<string> GetFlashcardStats([Description("Book: 'red' or 'green'")] string book) =>
        FlashcardLogic.TryBook(book, out var bk) ? J(await FlashcardLogic.StatsAsync(db, bk)) : J(new { error = "Invalid book. Use 'red' or 'green'." });
}

/// <summary>One part of a split_flashcard call, in the snake_case the MCP tools speak.</summary>
public sealed record FlashcardSplitInput(string content, Guid[]? prompt_ids);
