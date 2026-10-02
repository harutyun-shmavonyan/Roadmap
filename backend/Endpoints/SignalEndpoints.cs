using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;

namespace Roadmap.Api.Endpoints;

/// <summary>
/// The Stock Signals tab's REST side. The screener publishes over MCP; the tab reads here, ticks days read,
/// and records the reader's decision on each signal. Every rule lives in <see cref="SignalLogic"/>.
/// </summary>
public static class SignalEndpoints
{
    public static void MapSignalEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/signals").WithTags("Signals").RequireAuthorization();

        // The day list, newest first. Quiet days are rows too — "nothing fired" is the message.
        g.MapGet("/", async (int? days, RoadmapDbContext db) =>
        {
            var since = AppClock.Today().AddDays(-Math.Clamp(days ?? 90, 1, 3650));
            var rows = await db.SignalRuns.AsNoTracking()
                .Where(r => r.RunDate >= since)
                .OrderByDescending(r => r.RunDate).ToListAsync();
            return Results.Ok(rows.Select(SignalLogic.ToSummary));
        });

        g.MapGet("/latest", async (RoadmapDbContext db) =>
        {
            var run = await SignalLogic.FindRunAsync(db, null);
            return run is null ? Results.NotFound() : Results.Ok(SignalLogic.ToDto(run));
        });

        g.MapGet("/{date}", async (string date, RoadmapDbContext db) =>
        {
            if (!SignalLogic.TryParseDate(date, out _)) return Results.BadRequest(new { error = "Use YYYY-MM-DD." });
            var run = await SignalLogic.FindRunAsync(db, date);
            return run is null ? Results.NotFound() : Results.Ok(SignalLogic.ToDto(run));
        });

        // Same door as the MCP tool, for a run published from a script with the app token.
        g.MapPost("/", async (PublishSignalRunInput input, RoadmapDbContext db) =>
        {
            var error = SignalLogic.Validate(input);
            if (error is not null) return Results.BadRequest(new { error });
            var (run, action, carried, pruned) = await SignalLogic.PublishAsync(db, input);
            return Results.Ok(new { run = SignalLogic.ToSummary(run), action, carried, pruned });
        });

        g.MapPost("/{id:guid}/read", async (Guid id, RoadmapDbContext db) =>
        {
            var run = await db.SignalRuns.FirstOrDefaultAsync(r => r.Id == id);
            if (run is null) return Results.NotFound();
            await SignalLogic.MarkReadAsync(db, run, true);
            return Results.Ok(SignalLogic.ToSummary(run));
        });

        g.MapPost("/{id:guid}/unread", async (Guid id, RoadmapDbContext db) =>
        {
            var run = await db.SignalRuns.FirstOrDefaultAsync(r => r.Id == id);
            if (run is null) return Results.NotFound();
            await SignalLogic.MarkReadAsync(db, run, false);
            return Results.Ok(SignalLogic.ToSummary(run));
        });

        g.MapDelete("/{date}", async (string date, RoadmapDbContext db) =>
        {
            var run = await SignalLogic.FindRunAsync(db, date, track: true);
            if (run is null) return Results.NotFound();
            db.SignalRuns.Remove(run);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // The reader's decision on one signal: status and notes. PATCH semantics.
        g.MapPatch("/items/{id:guid}", async (Guid id, UpdateSignalRequest req, RoadmapDbContext db) =>
        {
            var s = await db.Signals.FirstOrDefaultAsync(x => x.Id == id);
            if (s is null) return Results.NotFound();
            var (updated, error) = await SignalLogic.UpdateAsync(db, s, req.Status, req.Notes);
            return error is not null ? Results.BadRequest(new { error }) : Results.Ok(SignalLogic.ToDto(updated!));
        });

        // The triage skill's verdict, by the screener's event id (also reachable over MCP as set_signal_triage).
        g.MapPost("/items/by-event/{eventId}/triage", async (string eventId, SetSignalTriageRequest req, RoadmapDbContext db) =>
        {
            var s = await SignalLogic.SetTriageAsync(db, eventId, req.Triage, req.Summary, req.Model);
            return s is null ? Results.NotFound(new { error = $"No signal with event_id '{eventId}'." }) : Results.Ok(SignalLogic.ToDto(s));
        });
    }
}
