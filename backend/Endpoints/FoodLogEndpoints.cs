using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Endpoints;

/// <summary>
/// The Nutrition tab's food log: what was eaten each day and what it added up to. Shaped like the
/// meal book — PUT replaces a whole entry, because the tab's form always sends every field.
/// </summary>
public static class FoodLogEndpoints
{
    public static void MapFoodLogEndpoints(this WebApplication app)
    {
        var log = app.MapGroup("/api/food-log").WithTags("FoodLog").RequireAuthorization();

        // Logged days in a range, newest first, each with its entries and totals. Defaults to the
        // last 30 days ending today.
        log.MapGet("/", async (string? from, string? to, RoadmapDbContext db) =>
        {
            var error = FoodLogLogic.TryParseRange(from, to, 30, out var f, out var t);
            if (error is not null) return Results.BadRequest(error);
            return Results.Ok(await FoodLogLogic.DaysAsync(db, f, t));
        });

        log.MapPost("/", async (SaveFoodLogEntryRequest req, RoadmapDbContext db) =>
        {
            var entry = new FoodLogEntry { Id = Guid.NewGuid() };
            var error = await Apply(db, entry, req);
            if (error is not null) return error;
            db.FoodLog.Add(entry);
            await db.SaveChangesAsync();
            return Results.Ok(FoodLogLogic.ToDto(entry));
        });

        log.MapPut("/{id:guid}", async (Guid id, SaveFoodLogEntryRequest req, RoadmapDbContext db) =>
        {
            var entry = await db.FoodLog.FirstOrDefaultAsync(f => f.Id == id);
            if (entry is null) return Results.NotFound();
            var error = await Apply(db, entry, req);
            if (error is not null) return error;
            entry.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(FoodLogLogic.ToDto(entry));
        });

        // Targets: maintenance calories and macro goals, each set effective from a date.
        log.MapGet("/targets", async (RoadmapDbContext db) =>
            Results.Ok((await FoodLogLogic.TargetsAsync(db)).Select(FoodLogLogic.ToDto)));

        log.MapPut("/targets", async (SaveNutritionTargetRequest req, RoadmapDbContext db) =>
        {
            if (!FoodLogLogic.TryParseDate(req.EffectiveFrom, out var from)) return Results.BadRequest("effectiveFrom must be yyyy-MM-dd.");
            var t = await FoodLogLogic.SetTargetsAsync(db, from, req.Calories, req.ProteinG, req.CarbsG, req.FatG);
            return Results.Ok(FoodLogLogic.ToDto(t));
        });

        log.MapDelete("/targets/{date}", async (string date, RoadmapDbContext db) =>
        {
            if (!FoodLogLogic.TryParseDate(date, out var d)) return Results.BadRequest("date must be yyyy-MM-dd.");
            var t = await db.NutritionTargets.FirstOrDefaultAsync(x => x.EffectiveFrom == d);
            if (t is null) return Results.NotFound();
            db.NutritionTargets.Remove(t);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        log.MapDelete("/{id:guid}", async (Guid id, RoadmapDbContext db) =>
        {
            var entry = await db.FoodLog.FirstOrDefaultAsync(f => f.Id == id);
            if (entry is null) return Results.NotFound();
            db.FoodLog.Remove(entry);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Write a request onto an entry, replacing every field (PUT semantics). A meal id fills the
    /// name and macros the request leaves blank, from the meal as it is now.
    /// </summary>
    static async Task<IResult?> Apply(RoadmapDbContext db, FoodLogEntry entry, SaveFoodLogEntryRequest req)
    {
        if (!FoodLogLogic.TryParseDate(req.Date, out var date)) return Results.BadRequest("date must be yyyy-MM-dd.");
        if (!FoodLogLogic.TryParseSlot(req.Slot, out var slot))
            return Results.BadRequest("slot must be Breakfast, Lunch, Dinner or Snack.");

        entry.Date = date;
        entry.Slot = slot ?? FoodLogLogic.SlotForNow();
        entry.Name = (req.Name ?? "").Trim();
        entry.Servings = req.Servings ?? 1;
        entry.Calories = FoodLogLogic.CleanMacro(req.Calories);
        entry.ProteinG = FoodLogLogic.CleanMacro(req.ProteinG);
        entry.CarbsG = FoodLogLogic.CleanMacro(req.CarbsG);
        entry.FatG = FoodLogLogic.CleanMacro(req.FatG);
        entry.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        entry.MealId = null;

        if (req.MealId is Guid mealId)
        {
            var meal = await db.Meals.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mealId);
            if (meal is null) return Results.BadRequest("meal not found.");
            FoodLogLogic.FillFromMeal(entry, meal);
        }

        var errors = FoodLogLogic.Validate(entry);
        return errors.Count > 0 ? Results.BadRequest(string.Join(" ", errors)) : null;
    }
}
