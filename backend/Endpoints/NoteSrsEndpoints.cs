using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Endpoints;

/// <summary>
/// Notes v2 for the Notes tab: see each note's prompts and how they are holding, fix or retire one,
/// add one by hand. Reviews themselves are recorded through the MCP tools during a chat session;
/// the session endpoint here is a read-only preview that never parks or unparks anything.
/// </summary>
public static class NoteSrsEndpoints
{
    public static void MapNoteSrsEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/notes").WithTags("NotesSrs").RequireAuthorization();

        g.MapGet("/srs/stats", async (RoadmapDbContext db) => Results.Ok(await NoteSrsLogic.StatsAsync(db)));

        g.MapGet("/srs/session", async (string? book, RoadmapDbContext db) =>
        {
            string? bk = null;
            if (!string.IsNullOrWhiteSpace(book))
            {
                bk = book.Trim().ToLowerInvariant();
                if (bk is not ("red" or "green")) return Results.BadRequest("Invalid book. Use 'red' or 'green'.");
            }
            return Results.Ok(await NoteSrsLogic.BuildSessionAsync(db, bk, persist: false));
        });

        g.MapGet("/srs/overview/{book}", async (string book, RoadmapDbContext db) =>
        {
            var bk = book.Trim().ToLowerInvariant();
            if (bk is not ("red" or "green")) return Results.BadRequest("Invalid book. Use 'red' or 'green'.");
            return Results.Ok(await NoteSrsLogic.OverviewAsync(db, bk));
        });

        g.MapGet("/{book}/{dayNumber:int}/prompts", async (string book, int dayNumber, RoadmapDbContext db) =>
        {
            var bk = book.Trim().ToLowerInvariant();
            if (bk is not ("red" or "green")) return Results.BadRequest("Invalid book. Use 'red' or 'green'.");
            var note = await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Book == bk && n.DayNumber == dayNumber);
            if (note is null) return Results.NotFound();
            var today = AppClock.Today();
            var prompts = await db.NotePrompts.AsNoTracking().Include(p => p.Note).Include(p => p.ReviewHistory)
                .Where(p => p.NoteId == note.Id).OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync();
            return Results.Ok(prompts.Select(p => NoteSrsLogic.ToDto(p, today, includeHistory: true)));
        });

        g.MapPost("/{book}/{dayNumber:int}/prompts", async (string book, int dayNumber, CreateNotePromptsRequest req, RoadmapDbContext db) =>
        {
            var bk = book.Trim().ToLowerInvariant();
            if (bk is not ("red" or "green")) return Results.BadRequest("Invalid book. Use 'red' or 'green'.");
            var note = await db.Notes.FirstOrDefaultAsync(n => n.Book == bk && n.DayNumber == dayNumber);
            if (note is null) return Results.NotFound();
            var (created, error) = await NoteSrsLogic.CreatePromptsAsync(db, note, req.Prompts);
            if (error is not null) return Results.BadRequest(error);
            await db.SaveChangesAsync();
            var today = AppClock.Today();
            return Results.Created("", created.Select(p => NoteSrsLogic.ToDto(p, today, includeHistory: false)));
        });

        g.MapPut("/prompts/{id:guid}", async (Guid id, UpdateNotePromptRequest req, RoadmapDbContext db) =>
        {
            var p = await db.NotePrompts.Include(x => x.Note).Include(x => x.ReviewHistory).FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            var error = NoteSrsLogic.ApplyUpdate(p, req.Question, req.Answer, req.State, req.Reset ?? false);
            if (error is not null) return Results.BadRequest(error);
            await db.SaveChangesAsync();
            return Results.Ok(NoteSrsLogic.ToDto(p, AppClock.Today(), includeHistory: true));
        });

        g.MapDelete("/prompts/{id:guid}", async (Guid id, RoadmapDbContext db) =>
        {
            var p = await db.NotePrompts.FindAsync(id);
            if (p is null) return Results.NotFound();
            db.NotePrompts.Remove(p);   // reviews cascade
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
