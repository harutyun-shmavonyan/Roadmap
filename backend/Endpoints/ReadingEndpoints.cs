using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Reading;
namespace Roadmap.Api.Endpoints;

public static class ReadingEndpoints
{
    public static void MapReadingEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/reading").RequireAuthorization();
        g.AddEndpointFilter(async (ctx, next) => {
            try { return await next(ctx); }
            catch (ArgumentException e) { return Results.BadRequest(new {error = e.Message}); }
            catch (KeyNotFoundException e) { return Results.NotFound(new {error = e.Message}); }
            catch (DbUpdateException) { return Results.Conflict(new {error = "Concurrent update. Reload and retry."}); }
        });
        g.MapGet("/context", (ReadingService s) => s.Context());
        g.MapGet("/history", (int? offset, int? limit, ReadingService s) => s.History(offset ?? 0, limit ?? 50));
        g.MapPost("/evaluations", (EvaluationInput x, ReadingService s) => s.SaveEvaluation(x));
        g.MapGet("/editions", (ReadingService s) => s.Editions());
        g.MapGet("/editions/{id:guid}", (Guid id, ReadingService s) => s.Edition(id));
        g.MapPost("/editions", (EditionInput x, ReadingService s) => s.Publish(x));
        g.MapPost("/editions/{id:guid}/read", (Guid id, ReadInput x, ReadingService s) => s.MarkRead(id, x));
        g.MapPatch("/evaluations/{id:guid}/feedback", (Guid id, FeedbackInput x, ReadingService s) => s.Feedback(id, x));
        g.MapPut("/settings", (SettingsInput x, ReadingService s) => s.Settings(x));
    }
}
