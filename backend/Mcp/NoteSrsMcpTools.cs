using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

/// <summary>
/// Notes v2 — the spaced-repetition surface over daily notes. A note is split into atomic prompts
/// when it is written (<c>create_note_prompts</c>); each day the quiz pulls what is due under the
/// daily cap (<c>get_due_note_prompts</c>) and records how each went (<c>record_note_prompt_review</c>).
/// Scheduling is deliberately NOT exposed: the caller supplies a grade and the server decides when the
/// prompt comes back (FSRS, see <see cref="Fsrs"/>), so an assistant cannot invent its own intervals.
/// </summary>
[McpServerToolType]
public sealed class NoteSrsMcpTools(RoadmapDbContext db)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    private static bool TryBook(string? book, out string normalized)
    {
        normalized = book?.Trim().ToLowerInvariant() ?? "";
        return normalized is "red" or "green";
    }

    private static object NoteRef(Note n) => new { book = n.Book, day_number = n.DayNumber, entry_date = n.EntryDate.ToString("yyyy-MM-dd") };

    // ===== Prompts on a note =====

    [McpServerTool(Name = "create_note_prompts"), Description(
        "Attach question/answer prompts to a daily note so it enters spaced repetition (Notes v2). Call this right after " +
        "add_note, with 1-3 atomic prompts extracted from the note (hard ceiling 5 per note). One fact per prompt; the " +
        "question must be specific and contain no hint of the answer; the answer must be checkable. Writing the note counts " +
        "as the first exposure, so each new prompt is already scheduled: first review in about 4 days. Nothing is asked today. " +
        "A note with nothing worth recalling (a reflection, a plan) should get zero prompts — simply do not call this. " +
        "Returns the created prompts and their due dates.")]
    public async Task<string> CreateNotePrompts(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The note's day_number")] int number,
        [Description("The prompts: each { question, answer }")] NotePromptInput[] prompts)
    {
        if (!TryBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Book == bk && n.DayNumber == number);
        if (note is null) return J(new { error = "Note not found" });

        var (created, error) = await NoteSrsLogic.CreatePromptsAsync(db, note, prompts);
        if (error is not null) return J(new { error });
        await db.SaveChangesAsync();

        var today = AppClock.Today();
        var total = await db.NotePrompts.CountAsync(p => p.NoteId == note.Id);
        return J(new
        {
            status = "created",
            note = NoteRef(note),
            created = created.Select(p => new { prompt_id = p.Id, p.Question, p.Answer, due_on = p.DueOn.ToString("yyyy-MM-dd") }),
            total_prompts_on_note = total,
            first_review_in_days = created.Count == 0 ? 0 : created[0].DueOn.DayNumber - today.DayNumber,
        });
    }

    [McpServerTool(Name = "list_note_prompts"), Description(
        "The prompts attached to one daily note, with their schedule: state (Active | Parked | Suspended), stability in days, " +
        "predicted recall today, next due date, lapses. Use it before create_note_prompts to avoid duplicating a fact, or to find a prompt_id to edit.")]
    public async Task<string> ListNotePrompts(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The note's day_number")] int number,
        [Description("Include every recorded review of each prompt (default false)")] bool include_history = false)
    {
        if (!TryBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        var note = await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Book == bk && n.DayNumber == number);
        if (note is null) return J(new { error = "Note not found" });

        IQueryable<NotePrompt> q = db.NotePrompts.AsNoTracking().Include(p => p.Note).Where(p => p.NoteId == note.Id);
        if (include_history) q = q.Include(p => p.ReviewHistory);
        var list = await q.OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync();
        var today = AppClock.Today();
        return J(new { note = NoteRef(note), count = list.Count, prompts = list.Select(p => NoteSrsLogic.ToDto(p, today, include_history)) });
    }

    [McpServerTool(Name = "update_note_prompt"), Description(
        "Edit a prompt. Only the fields you pass change. state: Active | Parked | Suspended (Suspended takes it out of rotation; " +
        "Active puts it back). reset=true restarts the schedule as if the prompt were just written — use it together with a rewritten " +
        "question/answer when a leech was suspended, so the fixed prompt starts fresh instead of inheriting eight lapses.")]
    public async Task<string> UpdateNotePrompt(
        [Description("Prompt UUID")] Guid prompt_id,
        [Description("New question")] string? question = null,
        [Description("New answer")] string? answer = null,
        [Description("Active | Parked | Suspended")] string? state = null,
        [Description("Restart the schedule from scratch (default false)")] bool reset = false)
    {
        var p = await db.NotePrompts.Include(x => x.Note).FirstOrDefaultAsync(x => x.Id == prompt_id);
        if (p is null) return J(new { error = "prompt not found" });
        var error = NoteSrsLogic.ApplyUpdate(p, question, answer, state, reset);
        if (error is not null) return J(new { error });
        await db.SaveChangesAsync();
        return J(new { status = "updated", prompt = NoteSrsLogic.ToDto(p, AppClock.Today(), includeHistory: false) });
    }

    [McpServerTool(Name = "delete_note_prompt"), Description("Permanently delete a prompt and its review history. The note itself is untouched.")]
    public async Task<string> DeleteNotePrompt([Description("Prompt UUID")] Guid prompt_id)
    {
        var p = await db.NotePrompts.FindAsync(prompt_id);
        if (p is null) return J(new { error = "prompt not found" });
        db.NotePrompts.Remove(p);
        await db.SaveChangesAsync();
        return J(new { status = "deleted", prompt_id, question = p.Question });
    }

    // ===== The daily session =====

    [McpServerTool(Name = "get_due_note_prompts"), Description(
        "Today's review queue for Notes v2, already triaged under the daily cap of " +
        "25 questions (remaining = cap minus what was already recorded today, across both books). Ask the prompts IN THE ORDER " +
        "RETURNED: yesterday's lapses lead, then due prompts by predicted recall, highest first. Each prompt carries its answer for " +
        "grading — never show it before the learner answers. Record every answer with record_note_prompt_review; a prompt you skip " +
        "stays due. Calling this again the same day returns what is still due within the remaining budget, so it is safe to resume. " +
        "Side effects: due prompts that did not fit and have fallen below 50% predicted recall are parked (not deleted); on a day " +
        "with spare slots parked prompts are pulled back in. The response says how many of each happened.")]
    public async Task<string> GetDueNotePrompts(
        [Description("Restrict to one book: 'red' or 'green'. Omit for both (the default — the cap is shared anyway).")] string? book = null)
    {
        string? bk = null;
        if (!string.IsNullOrWhiteSpace(book))
        {
            if (!TryBook(book, out var b)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
            bk = b;
        }
        var session = await NoteSrsLogic.BuildSessionAsync(db, bk, persist: true);
        return J(session);
    }

    [McpServerTool(Name = "record_note_prompt_review"), Description(
        "Record how one prompt went and let the server reschedule it (FSRS). grade: 'again' = forgot or wrong (comes back tomorrow, " +
        "stability collapses); 'hard' = right in part or with real effort; 'good' = recalled correctly; 'easy' = instant and complete, " +
        "or the learner answered 'O' meaning they know it perfectly (pass answer='O'). Returns the new stability, the next due date, " +
        "how many questions remain under today's cap, and leech=true when this lapse suspended the prompt (offer to rewrite it with " +
        "update_note_prompt reset=true).")]
    public async Task<string> RecordNotePromptReview(
        [Description("Prompt UUID (from get_due_note_prompts)")] Guid prompt_id,
        [Description("again | hard | good | easy")] string grade,
        [Description("What the learner actually answered ('O' when they declared it known)")] string? answer = null,
        [Description("Why this grade — one line")] string? note = null)
    {
        if (!NoteSrsLogic.TryParseGrade(grade, out var g)) return J(new { error = $"invalid grade '{grade}'. Use again | hard | good | easy." });
        var p = await db.NotePrompts.Include(x => x.Note).FirstOrDefaultAsync(x => x.Id == prompt_id);
        if (p is null) return J(new { error = "prompt not found" });

        var today = AppClock.Today();
        var review = NoteSrsLogic.ApplyReview(p, g, answer, note, today, DateTime.UtcNow);
        db.NotePromptReviews.Add(review);
        await db.SaveChangesAsync();

        var askedToday = await NoteSrsLogic.AskedTodayAsync(db, today);
        return J(new
        {
            status = g == FsrsGrade.Again ? "failed" : "passed",
            grade = g.ToString(),
            question = p.Question,
            note = p.Note is null ? null : NoteRef(p.Note),
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
            leech = NoteSrsLogic.IsLeech(p),
            asked_today = askedToday,
            remaining_today = Math.Max(0, NoteSrsLogic.DailyCap - askedToday),
            daily_cap = NoteSrsLogic.DailyCap,
        });
    }

    // ===== Coverage =====

    [McpServerTool(Name = "list_notes_without_prompts"), Description(
        "Daily notes that have no prompts yet, newest first, with their content — the backfill list. Feed existing notes into Notes v2 " +
        "a few at a time (each gets 1-3 prompts via create_note_prompts), never all at once: every prompt created today is due in 4 days.")]
    public async Task<string> ListNotesWithoutPrompts(
        [Description("Restrict to one book: 'red' or 'green'. Omit for both.")] string? book = null,
        [Description("Max notes to return (default 5)")] int limit = 5)
    {
        string? bk = null;
        if (!string.IsNullOrWhiteSpace(book))
        {
            if (!TryBook(book, out var b)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
            bk = b;
        }
        if (limit is < 1 or > 50) limit = 5;

        var q = db.Notes.AsNoTracking().Where(n => !db.NotePrompts.Any(p => p.NoteId == n.Id));
        if (bk is not null) q = q.Where(n => n.Book == bk);
        var total = await q.CountAsync();
        var notes = await q.OrderByDescending(n => n.EntryDate).ThenByDescending(n => n.DayNumber).Take(limit).ToListAsync();
        return J(new
        {
            total_without_prompts = total,
            returned = notes.Count,
            notes = notes.Select(n => new { book = n.Book, day_number = n.DayNumber, entry_date = n.EntryDate.ToString("yyyy-MM-dd"), content = n.Content }),
        });
    }

    [McpServerTool(Name = "get_note_srs_stats"), Description(
        "Totals for Notes v2: prompts by state, notes with and without prompts, due today, asked today vs the daily cap, how many new " +
        "prompts a day the cap can carry, true retention over 30 days (should sit near the 0.9 target), lapses, and the due count for each of the next 7 days.")]
    public async Task<string> GetNoteSrsStats() => J(await NoteSrsLogic.StatsAsync(db));
}
