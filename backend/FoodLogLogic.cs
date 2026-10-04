using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// Everything about the food log that the REST endpoints and the MCP tools must agree on: how a
/// date and a slot are read, what an entry logged from the meal book copies, what is valid, and
/// how a day adds up. Static and shared for the same reason as <c>ExperienceLogic</c> — two
/// surfaces write the same rows, and the only way they cannot drift is to not each have a copy.
/// </summary>
public static class FoodLogLogic
{
    public const int MaxNameLength = 200;
    public const int MaxNoteLength = 512;
    public const double MaxServings = 50;
    /// <summary>The longest range one read returns, so a typo in a date cannot load years of rows.</summary>
    public const int MaxRangeDays = 366;

    // ── reading input ──

    /// <summary>A yyyy-MM-dd date, or today (Asia/Yerevan) when blank. Anything else is an error.</summary>
    public static bool TryParseDate(string? raw, out DateOnly date)
    {
        date = AppClock.Today();
        if (string.IsNullOrWhiteSpace(raw)) return true;
        return DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    /// <summary>Breakfast | Lunch | Dinner | Snack, case-insensitive; null when blank.</summary>
    public static bool TryParseSlot(string? raw, out MealSlot? slot)
    {
        slot = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (!Enum.TryParse<MealSlot>(raw.Trim(), ignoreCase: true, out var s) || !Enum.IsDefined(s)) return false;
        slot = s;
        return true;
    }

    /// <summary>The slot a log written right now most likely belongs to, by the local hour.</summary>
    public static MealSlot SlotForNow()
    {
        var h = AppClock.Now().Hour;
        return h < 11 ? MealSlot.Breakfast : h < 15 ? MealSlot.Lunch : h < 18 ? MealSlot.Snack
            : h < 23 ? MealSlot.Dinner : MealSlot.Snack;
    }

    /// <summary>Negative macros are meaningless; clamp to zero and keep one decimal.</summary>
    public static double? CleanMacro(double? v) =>
        v is null || double.IsNaN(v.Value) || double.IsInfinity(v.Value) ? null : Math.Round(Math.Max(0, v.Value), 1);

    /// <summary>
    /// Everything wrong with an entry as it would be saved, or nothing. Collected rather than
    /// thrown one at a time, so a caller fixing a form fixes it in one pass.
    /// </summary>
    public static List<string> Validate(FoodLogEntry e)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(e.Name)) errors.Add("name is required (or log from a meal with meal_id).");
        else if (e.Name.Length > MaxNameLength) errors.Add($"name must be at most {MaxNameLength} characters.");
        if (!(e.Servings > 0) || e.Servings > MaxServings) errors.Add($"servings must be above 0 and at most {MaxServings}.");
        if (e.Note is { Length: > MaxNoteLength }) errors.Add($"note must be at most {MaxNoteLength} characters.");
        return errors;
    }

    /// <summary>
    /// Fill what an entry logged from the meal book does not say from the meal itself: its name and
    /// its per-serving macros. Only blanks are filled — a figure given explicitly wins — and the
    /// copy is a snapshot, so a later edit to the meal never reaches history.
    /// </summary>
    public static void FillFromMeal(FoodLogEntry e, Meal meal)
    {
        e.MealId = meal.Id;
        if (string.IsNullOrWhiteSpace(e.Name)) e.Name = meal.Name;
        e.Calories ??= meal.Calories;
        e.ProteinG ??= meal.ProteinG;
        e.CarbsG ??= meal.CarbsG;
        e.FatG ??= meal.FatG;
    }

    // ── adding up ──

    public static MacroTotals TotalsOf(FoodLogEntry e) => new(
        Times(e.Calories, e.Servings), Times(e.ProteinG, e.Servings),
        Times(e.CarbsG, e.Servings), Times(e.FatG, e.Servings));

    static double? Times(double? perServing, double servings) =>
        perServing is null ? null : Math.Round(perServing.Value * servings, 1);

    /// <summary>Sum of the known figures; a macro no entry knows stays null rather than reading 0.</summary>
    public static MacroTotals Sum(IEnumerable<MacroTotals> parts)
    {
        var list = parts.ToList();
        static double? Add(IEnumerable<double?> xs)
        {
            var known = xs.Where(x => x is not null).Select(x => x!.Value).ToList();
            return known.Count == 0 ? null : Math.Round(known.Sum(), 1);
        }
        return new MacroTotals(Add(list.Select(t => t.Calories)), Add(list.Select(t => t.ProteinG)),
            Add(list.Select(t => t.CarbsG)), Add(list.Select(t => t.FatG)));
    }

    public static bool IsMissingMacros(FoodLogEntry e) =>
        e.Calories is null || e.ProteinG is null || e.CarbsG is null || e.FatG is null;

    public static FoodLogEntryDto ToDto(FoodLogEntry e) => new(
        e.Id, e.Date.ToString("yyyy-MM-dd"), e.Slot.ToString(), e.MealId, e.Name, e.Servings,
        e.Calories, e.ProteinG, e.CarbsG, e.FatG, TotalsOf(e), IsMissingMacros(e), e.Note,
        e.CreatedAt, e.UpdatedAt);

    /// <summary>
    /// The entries grouped into days, newest day first; within a day, by slot then time logged. Each
    /// day carries the targets in effect on it (see <see cref="TargetOn"/>), or null if none were set.
    /// </summary>
    public static List<FoodLogDayDto> ToDays(IEnumerable<FoodLogEntry> entries, IReadOnlyList<NutritionTarget>? targets = null) =>
        entries.GroupBy(e => e.Date).OrderByDescending(g => g.Key).Select(g =>
        {
            var ordered = g.OrderBy(e => e.Slot).ThenBy(e => e.CreatedAt).ToList();
            var t = targets is null ? null : TargetOn(targets, g.Key);
            return new FoodLogDayDto(g.Key.ToString("yyyy-MM-dd"), Sum(ordered.Select(TotalsOf)),
                ordered.Count, ordered.Count(IsMissingMacros), ordered.Select(ToDto).ToList(),
                t is null ? null : new MacroTotals(t.Calories, t.ProteinG, t.CarbsG, t.FatG));
        }).ToList();

    /// <summary>Every logged day in [from, to], newest first. Days with nothing logged are left out.</summary>
    public static async Task<List<FoodLogDayDto>> DaysAsync(RoadmapDbContext db, DateOnly from, DateOnly to) =>
        ToDays(await db.FoodLog.AsNoTracking().Where(f => f.Date >= from && f.Date <= to).ToListAsync(),
            await TargetsAsync(db));

    // ── targets ──

    /// <summary>Every set of targets, oldest first.</summary>
    public static Task<List<NutritionTarget>> TargetsAsync(RoadmapDbContext db) =>
        db.NutritionTargets.AsNoTracking().OrderBy(t => t.EffectiveFrom).ToListAsync();

    /// <summary>The targets in effect on a day: the latest set whose start is on or before it.</summary>
    public static NutritionTarget? TargetOn(IReadOnlyList<NutritionTarget> oldestFirst, DateOnly day) =>
        oldestFirst.LastOrDefault(t => t.EffectiveFrom <= day);

    public static NutritionTargetDto ToDto(NutritionTarget t) =>
        new(t.EffectiveFrom.ToString("yyyy-MM-dd"), t.Calories, t.ProteinG, t.CarbsG, t.FatG);

    /// <summary>
    /// Set the targets from a date: replaces the set starting that day, or adds one. Figures are
    /// cleaned like macros (no negatives, one decimal); a zero target is treated as "none".
    /// </summary>
    public static async Task<NutritionTarget> SetTargetsAsync(RoadmapDbContext db, DateOnly from,
        double? calories, double? protein, double? carbs, double? fat)
    {
        static double? Clean(double? v) => CleanMacro(v) is double d && d > 0 ? d : null;
        var t = await db.NutritionTargets.FirstOrDefaultAsync(x => x.EffectiveFrom == from);
        if (t is null) { t = new NutritionTarget { Id = Guid.NewGuid(), EffectiveFrom = from }; db.NutritionTargets.Add(t); }
        t.Calories = Clean(calories);
        t.ProteinG = Clean(protein);
        t.CarbsG = Clean(carbs);
        t.FatG = Clean(fat);
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return t;
    }

    /// <summary>
    /// Read a from/to pair: blank to is today, blank from is <paramref name="defaultDays"/> back from
    /// to. Refuses an inverted range or one longer than <see cref="MaxRangeDays"/>.
    /// </summary>
    public static string? TryParseRange(string? fromRaw, string? toRaw, int defaultDays,
        out DateOnly from, out DateOnly to)
    {
        from = default;
        if (!TryParseDate(toRaw, out to)) return "to must be yyyy-MM-dd.";
        if (string.IsNullOrWhiteSpace(fromRaw)) from = to.AddDays(-(Math.Max(1, defaultDays) - 1));
        else if (!TryParseDate(fromRaw, out from)) return "from must be yyyy-MM-dd.";
        if (from > to) return "from must not be after to.";
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays) return $"a range can span at most {MaxRangeDays} days.";
        return null;
    }
}
