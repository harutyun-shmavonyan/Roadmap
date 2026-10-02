using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Endpoints;

/// <summary>
/// Notes v2 for the tab: browse cards, read and fix prompts, run the flashcard review in the app.
/// Everything goes through <see cref="FlashcardLogic"/>, the same code the MCP tools use.
/// </summary>
public static class FlashcardEndpoints
{
    private static IResult BadBook() => Results.BadRequest("Invalid book. Use 'red' or 'green'.");

    public static void MapFlashcardEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/flashcards").WithTags("Flashcards").RequireAuthorization();

        // Cards newest first. Filters: book, an exact date, a from/to range, a text search, "no prompts yet".
        g.MapGet("/", async (string? book, string? date, string? from, string? to, string? search, int? limit, bool? withoutPrompts, RoadmapDbContext db) =>
        {
            string? bk = null;
            if (!string.IsNullOrWhiteSpace(book)) { if (!FlashcardLogic.TryBook(book, out bk)) return BadBook(); }
            DateOnly? d = null, f = null, t = null;
            if (!string.IsNullOrWhiteSpace(date)) { if (!DateOnly.TryParse(date, out var x)) return Results.BadRequest("Invalid date."); d = x; }
            if (!string.IsNullOrWhiteSpace(from)) { if (!DateOnly.TryParse(from, out var x)) return Results.BadRequest("Invalid from."); f = x; }
            if (!string.IsNullOrWhiteSpace(to)) { if (!DateOnly.TryParse(to, out var x)) return Results.BadRequest("Invalid to."); t = x; }
            var n = limit is > 0 and <= 5000 ? limit.Value : 2000;
            var (_, cards) = await FlashcardLogic.ListCardsAsync(db, bk, d, f, t, search, withoutPrompts ?? false, n);
            return Results.Ok(cards);
        });

        g.MapGet("/stats", async (RoadmapDbContext db) => Results.Ok(await FlashcardLogic.StatsAsync(db)));

        // The dashboard's memory snapshot: stability and days-since-seen per prompt.
        g.MapGet("/memory", async (string? book, RoadmapDbContext db) =>
        {
            string? bk = null;
            if (!string.IsNullOrWhiteSpace(book)) { if (!FlashcardLogic.TryBook(book, out bk)) return BadBook(); }
            return Results.Ok(await FlashcardLogic.MemoryAsync(db, bk));
        });

        // Preview of today's queue — no parking, no unparking.
        g.MapGet("/session", async (string? book, RoadmapDbContext db) =>
        {
            string? bk = null;
            if (!string.IsNullOrWhiteSpace(book)) { if (!FlashcardLogic.TryBook(book, out bk)) return BadBook(); }
            return Results.Ok(await FlashcardLogic.BuildSessionAsync(db, bk, persist: false));
        });

        // Start or resume today's session in the app: same triage and side effects as the MCP tool.
        g.MapPost("/session", async (string? book, RoadmapDbContext db) =>
        {
            string? bk = null;
            if (!string.IsNullOrWhiteSpace(book)) { if (!FlashcardLogic.TryBook(book, out bk)) return BadBook(); }
            return Results.Ok(await FlashcardLogic.BuildSessionAsync(db, bk, persist: true));
        });

        g.MapPost("/", async (CreateFlashcardRequest req, RoadmapDbContext db) =>
        {
            if (!FlashcardLogic.TryBook(req.Book, out var bk)) return BadBook();
            if (string.IsNullOrWhiteSpace(req.Content) && (req.Prompts is null || req.Prompts.Count == 0))
                return Results.BadRequest("content is required");
            DateOnly date;
            if (string.IsNullOrWhiteSpace(req.EntryDate)) date = AppClock.Today();
            else if (!DateOnly.TryParse(req.EntryDate, out date)) return Results.BadRequest("Invalid entryDate. Use YYYY-MM-DD.");

            var card = await FlashcardLogic.CreateCardAsync(db, bk, req.Content ?? "", date);
            if (req.Prompts is { Count: > 0 })
            {
                var (_, error) = await FlashcardLogic.CreatePromptsAsync(db, card, req.Prompts, req.Backfill ?? date < AppClock.Today());
                if (error is not null) return Results.BadRequest(error);
                await db.SaveChangesAsync();
            }
            var full = await db.Flashcards.AsNoTracking().Include(c => c.Prompts).FirstAsync(c => c.Id == card.Id);
            var dto = FlashcardLogic.ToCardDto(full, AppClock.Today(), includeHistory: false);
            return Results.Created($"/api/flashcards/{card.Id}", dto);
        });

        g.MapGet("/{id:guid}", async (Guid id, RoadmapDbContext db) =>
        {
            var card = await db.Flashcards.AsNoTracking().Include(c => c.Prompts).ThenInclude(p => p.ReviewHistory)
                .FirstOrDefaultAsync(c => c.Id == id);
            return card is null ? Results.NotFound() : Results.Ok(FlashcardLogic.ToCardDto(card, AppClock.Today(), includeHistory: true));
        });

        g.MapPut("/{id:guid}", async (Guid id, UpdateFlashcardRequest req, RoadmapDbContext db) =>
        {
            var card = await db.Flashcards.Include(c => c.Prompts).FirstOrDefaultAsync(c => c.Id == id);
            if (card is null) return Results.NotFound();
            card.Content = req.Content ?? "";
            card.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(FlashcardLogic.ToCardDto(card, AppClock.Today(), includeHistory: false));
        });

        g.MapDelete("/{id:guid}", async (Guid id, RoadmapDbContext db) =>
        {
            var card = await db.Flashcards.FindAsync(id);
            if (card is null) return Results.NotFound();
            db.Flashcards.Remove(card);   // prompts and reviews cascade
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/split", async (Guid id, SplitFlashcardRequest req, RoadmapDbContext db) =>
        {
            var (cards, error) = await FlashcardLogic.SplitCardAsync(db, id, req.Parts);
            if (error is not null) return error == "flashcard not found" ? Results.NotFound() : Results.BadRequest(error);
            var ids = cards.Select(c => c.Id).ToList();
            var full = await db.Flashcards.AsNoTracking().Include(c => c.Prompts).Where(c => ids.Contains(c.Id)).ToListAsync();
            var today = AppClock.Today();
            return Results.Ok(full.OrderBy(c => c.CreatedAt).Select(c => FlashcardLogic.ToCardDto(c, today, includeHistory: false)));
        });

        g.MapPost("/{id:guid}/prompts", async (Guid id, AddFlashcardPromptsRequest req, RoadmapDbContext db) =>
        {
            var card = await db.Flashcards.FirstOrDefaultAsync(c => c.Id == id);
            if (card is null) return Results.NotFound();
            var (created, error) = await FlashcardLogic.CreatePromptsAsync(db, card, req.Prompts, req.Backfill ?? card.EntryDate < AppClock.Today());
            if (error is not null) return Results.BadRequest(error);
            await db.SaveChangesAsync();
            var today = AppClock.Today();
            return Results.Created("", created.Select(p => FlashcardLogic.ToPromptDto(p, today, includeHistory: false)));
        });

        g.MapPut("/prompts/{id:guid}", async (Guid id, UpdateFlashcardPromptRequest req, RoadmapDbContext db) =>
        {
            var p = await db.FlashcardPrompts.Include(x => x.Flashcard).Include(x => x.ReviewHistory).FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            var error = FlashcardLogic.ApplyUpdate(p, req.Question, req.Answer, req.State, req.Reset ?? false);
            if (error is not null) return Results.BadRequest(error);
            await db.SaveChangesAsync();
            return Results.Ok(FlashcardLogic.ToPromptDto(p, AppClock.Today(), includeHistory: true));
        });

        g.MapDelete("/prompts/{id:guid}", async (Guid id, RoadmapDbContext db) =>
        {
            var p = await db.FlashcardPrompts.FindAsync(id);
            if (p is null) return Results.NotFound();
            db.FlashcardPrompts.Remove(p);   // reviews cascade
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // The flashcard view's self-graded answer. In chat the skill grades; the server owns the schedule either way.
        g.MapPost("/prompts/{id:guid}/review", async (Guid id, RecordFlashcardReviewRequest req, RoadmapDbContext db) =>
        {
            if (!FlashcardLogic.TryParseGrade(req.Grade, out var grade))
                return Results.BadRequest($"invalid grade '{req.Grade}'. Use again | hard | good | easy.");
            var p = await db.FlashcardPrompts.Include(x => x.Flashcard).FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();

            var today = AppClock.Today();
            var review = FlashcardLogic.ApplyReview(p, grade, req.Answer, req.Note, today, DateTime.UtcNow);
            db.FlashcardReviews.Add(review);
            await db.SaveChangesAsync();

            var askedToday = await FlashcardLogic.AskedTodayAsync(db, today);
            return Results.Ok(new FlashcardReviewResultDto(
                FlashcardLogic.ToPromptDto(p, today, includeHistory: false), grade.ToString(), grade != FsrsGrade.Again,
                Math.Round(review.Retrievability, 3), review.ElapsedDays, p.DueOn.DayNumber - today.DayNumber,
                FlashcardLogic.IsLeech(p), askedToday, Math.Max(0, FlashcardLogic.DailyCap - askedToday), FlashcardLogic.DailyCap));
        });
    }
}
