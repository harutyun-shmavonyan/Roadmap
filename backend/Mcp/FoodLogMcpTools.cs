using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

/// <summary>
/// The food log behind the Nutrition tab's history — the same rows the tab writes via
/// /api/food-log, through the same <see cref="FoodLogLogic"/>.
///
/// One deliberate difference from REST, the same as the meal book's: PUT there replaces a whole
/// entry, but <see cref="UpdateEntry"/> patches — an assistant rarely restates what it is not
/// changing — so a null argument keeps the stored value.
/// </summary>
[McpServerToolType]
public sealed class FoodLogMcpTools(RoadmapDbContext db)
{
    static readonly JsonSerializerOptions Out = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    static string J(object? v) => JsonSerializer.Serialize(v, Out);

    const string SlotHelp = "Breakfast | Lunch | Dinner | Snack";

    [McpServerTool(Name = "log_food"), Description(
        "Record something the user ate in the Nutrition tab's food log (its day-by-day history). " +
        "Either log a meal from the meal book with meal_id — its name and per-serving macros are copied, " +
        "as a snapshot, unless given here — or log anything else by name with its macros. Macros are " +
        "PER SERVING and multiplied by servings; estimate them when the user does not know (and say so " +
        "in note), since an unknown macro leaves the day's totals incomplete. Use list_meals to find a " +
        "meal_id. Date defaults to today and slot to the one matching the current hour (Asia/Yerevan).")]
    public async Task<string> LogFood(
        [Description("What was eaten, e.g. \"2 eggs on rye toast\". Optional when meal_id is given.")] string? name = null,
        [Description("Meal-book meal UUID to log from (see list_meals)")] Guid? meal_id = null,
        [Description("yyyy-MM-dd; default today")] string? date = null,
        [Description(SlotHelp + "; default by the current hour")] string? slot = null,
        [Description("How many servings; default 1. Fractions are fine, e.g. 0.5 or 1.5.")] double? servings = null,
        [Description("Calories per serving")] double? calories = null,
        [Description("Protein grams per serving")] double? protein_g = null,
        [Description("Carb grams per serving")] double? carbs_g = null,
        [Description("Fat grams per serving")] double? fat_g = null,
        [Description("Optional note, e.g. \"restaurant portion, macros estimated\"")] string? note = null)
    {
        if (!FoodLogLogic.TryParseDate(date, out var day)) return J(new { error = "date must be yyyy-MM-dd" });
        if (!FoodLogLogic.TryParseSlot(slot, out var s)) return J(new { error = $"invalid slot '{slot}'. Use {SlotHelp}." });

        var entry = new FoodLogEntry
        {
            Id = Guid.NewGuid(),
            Date = day,
            Slot = s ?? FoodLogLogic.SlotForNow(),
            Name = (name ?? "").Trim(),
            Servings = servings ?? 1,
            Calories = FoodLogLogic.CleanMacro(calories),
            ProteinG = FoodLogLogic.CleanMacro(protein_g),
            CarbsG = FoodLogLogic.CleanMacro(carbs_g),
            FatG = FoodLogLogic.CleanMacro(fat_g),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
        if (meal_id is Guid mid)
        {
            var meal = await db.Meals.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mid);
            if (meal is null) return J(new { error = "meal not found" });
            FoodLogLogic.FillFromMeal(entry, meal);
        }
        var errors = FoodLogLogic.Validate(entry);
        if (errors.Count > 0) return J(new { error = string.Join(" ", errors) });

        db.FoodLog.Add(entry);
        await db.SaveChangesAsync();
        var dayNow = (await FoodLogLogic.DaysAsync(db, day, day)).Single();
        return J(new { status = "logged", entry = FoodLogLogic.ToDto(entry), dayTotals = dayNow.Totals, dayIncompleteEntries = dayNow.IncompleteEntries });
    }

    [McpServerTool(Name = "list_food_log"), Description(
        "The food log, day by day, newest first: what was eaten each day (by slot) and the day's total " +
        "calories, protein, carbs and fat. Days with nothing logged are left out. incompleteEntries counts " +
        "entries with an unknown macro, so that day's totals are a lower bound. Defaults to the last 7 days.")]
    public async Task<string> ListFoodLog(
        [Description("yyyy-MM-dd, first day; default `days` back from `to`")] string? from = null,
        [Description("yyyy-MM-dd, last day; default today")] string? to = null,
        [Description("How many days back when from is omitted; default 7")] int days = 7)
    {
        var error = FoodLogLogic.TryParseRange(from, to, days, out var f, out var t);
        if (error is not null) return J(new { error });
        var list = await FoodLogLogic.DaysAsync(db, f, t);
        return J(new { from = f.ToString("yyyy-MM-dd"), to = t.ToString("yyyy-MM-dd"), loggedDays = list.Count, days = list });
    }

    [McpServerTool(Name = "update_food_log_entry"), Description(
        "Change a food-log entry. Patch semantics: only the arguments given change; a null keeps the stored " +
        "value, and an empty note clears it. Macros are per serving.")]
    public async Task<string> UpdateEntry(
        [Description("Entry UUID (from list_food_log or log_food)")] Guid entry_id,
        string? name = null,
        [Description("yyyy-MM-dd")] string? date = null,
        [Description(SlotHelp)] string? slot = null,
        double? servings = null,
        [Description("Calories per serving")] double? calories = null,
        [Description("Protein grams per serving")] double? protein_g = null,
        [Description("Carb grams per serving")] double? carbs_g = null,
        [Description("Fat grams per serving")] double? fat_g = null,
        string? note = null)
    {
        var entry = await db.FoodLog.FirstOrDefaultAsync(e => e.Id == entry_id);
        if (entry is null) return J(new { error = "entry not found" });

        if (date is not null)
        {
            if (!FoodLogLogic.TryParseDate(date, out var d)) return J(new { error = "date must be yyyy-MM-dd" });
            entry.Date = d;
        }
        if (slot is not null)
        {
            if (!FoodLogLogic.TryParseSlot(slot, out var s) || s is null) return J(new { error = $"invalid slot '{slot}'. Use {SlotHelp}." });
            entry.Slot = s.Value;
        }
        if (name is not null) entry.Name = name.Trim();
        if (servings is not null) entry.Servings = servings.Value;
        if (calories is not null) entry.Calories = FoodLogLogic.CleanMacro(calories);
        if (protein_g is not null) entry.ProteinG = FoodLogLogic.CleanMacro(protein_g);
        if (carbs_g is not null) entry.CarbsG = FoodLogLogic.CleanMacro(carbs_g);
        if (fat_g is not null) entry.FatG = FoodLogLogic.CleanMacro(fat_g);
        if (note is not null) entry.Note = note.Trim().Length == 0 ? null : note.Trim();

        var errors = FoodLogLogic.Validate(entry);
        if (errors.Count > 0) return J(new { error = string.Join(" ", errors) });
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(new { status = "updated", entry = FoodLogLogic.ToDto(entry) });
    }

    [McpServerTool(Name = "delete_food_log_entry"), Description("Remove one entry from the food log.")]
    public async Task<string> DeleteEntry([Description("Entry UUID")] Guid entry_id)
    {
        var entry = await db.FoodLog.FirstOrDefaultAsync(e => e.Id == entry_id);
        if (entry is null) return J(new { error = "entry not found" });
        db.FoodLog.Remove(entry);
        await db.SaveChangesAsync();
        return J(new { status = "deleted", id = entry_id });
    }
}
