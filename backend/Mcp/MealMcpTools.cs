using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

/// <summary>
/// CRUD over the meal book behind the Nutrition tab — the same store the UI writes to
/// via /api/meals.
///
/// One deliberate difference from the REST surface: the PUT there replaces a whole meal,
/// so a missing list clears it. That is wrong for an assistant, which rarely restates
/// every field it is not changing, so <see cref="UpdateMeal"/> patches instead — a null
/// argument keeps the stored value and an empty array is the explicit way to clear a list.
/// </summary>
[McpServerToolType]
public sealed class MealMcpTools(RoadmapDbContext db)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    private const string SlotHelp = "Breakfast | Lunch | Dinner | Snack";

    private static bool TryParseSlot(string? raw, MealSlot fallback, out MealSlot value)
    {
        if (string.IsNullOrWhiteSpace(raw)) { value = fallback; return true; }
        return Enum.TryParse(raw, ignoreCase: true, out value);
    }

    // Trim every line and drop the blanks — callers paste ragged lists.
    private static List<string> CleanList(IEnumerable<string>? raw) =>
        raw is null ? [] : [.. raw.Select(s => (s ?? "").Trim()).Where(s => s.Length > 0)];

    // Negative macros are meaningless; clamp rather than reject so a typo doesn't lose the meal.
    private static int? NonNegative(int? v) => v is null ? null : Math.Max(0, v.Value);

    private static MealDto ToDto(Meal m) => new(m.Id, m.Slot.ToString(), m.Name, m.Summary,
        m.Ingredients, m.Steps, m.Calories, m.ProteinG, m.CarbsG, m.FatG, m.PrepMinutes,
        m.Tags, m.IsFavorite, m.SortOrder, m.CreatedAt, m.UpdatedAt);

    /// <summary>Next free position at the end of a slot.</summary>
    private async Task<int> NextSortOrder(MealSlot slot) =>
        (await db.Meals.Where(m => m.Slot == slot).Select(m => (int?)m.SortOrder).MaxAsync() ?? -1) + 1;

    // ===== Read =====

    [McpServerTool(Name = "list_meals"), Description(
        "List saved meals from the Nutrition tab's meal book, favourites first within each slot. " +
        "Pass slot to narrow to one part of the day; omit it for the whole book.")]
    public async Task<string> ListMeals(
        [Description(SlotHelp + " — omit for all slots")] string? slot = null,
        [Description("Only meals tagged with this label, e.g. \"high-protein\" (case-insensitive)")] string? tag = null,
        [Description("Only starred meals")] bool favorites_only = false)
    {
        MealSlot? filter = null;
        if (!string.IsNullOrWhiteSpace(slot))
        {
            if (!Enum.TryParse<MealSlot>(slot, ignoreCase: true, out var parsed))
                return J(new { error = $"invalid slot '{slot}'. Use {SlotHelp}." });
            filter = parsed;
        }

        var q = db.Meals.AsNoTracking();
        if (filter is not null) q = q.Where(m => m.Slot == filter);
        if (favorites_only) q = q.Where(m => m.IsFavorite);

        var list = await q
            .OrderBy(m => m.Slot)
            .ThenByDescending(m => m.IsFavorite)
            .ThenBy(m => m.SortOrder)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync();

        // Tag matching is done in memory: the tags are a serialised list, not a queryable column.
        if (!string.IsNullOrWhiteSpace(tag))
            list = [.. list.Where(m => m.Tags.Any(t => string.Equals(t, tag.Trim(), StringComparison.OrdinalIgnoreCase)))];

        return J(new { count = list.Count, meals = list.Select(ToDto) });
    }

    [McpServerTool(Name = "get_meal"), Description("Get one meal in full — ingredients, steps, macros and tags.")]
    public async Task<string> GetMeal([Description("Meal UUID")] Guid meal_id)
    {
        var meal = await db.Meals.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meal_id);
        return meal is null ? J(new { error = "meal not found" }) : J(ToDto(meal));
    }

    // ===== Write =====

    [McpServerTool(Name = "create_meal"), Description(
        "Save a meal worth keeping to the Nutrition tab. This is a cookbook of what to eat, " +
        "not a food log — save a meal when it earns a repeat, not to record that it was eaten. " +
        "Every macro is optional and can be filled in later. The meal lands at the end of its slot.")]
    public async Task<string> CreateMeal(
        [Description("What it's called, e.g. \"Greek yogurt, berries & walnuts\"")] string name,
        [Description(SlotHelp + " (default Breakfast)")] string? slot = null,
        [Description("One line on why it earns its place")] string? summary = null,
        [Description("What goes in it, one entry per ingredient, with amounts")] string[]? ingredients = null,
        [Description("How it's made, one entry per step. Omit for assembly-only meals.")] string[]? steps = null,
        [Description("Calories per serving")] int? calories = null,
        [Description("Protein grams per serving")] int? protein_g = null,
        [Description("Carb grams per serving")] int? carbs_g = null,
        [Description("Fat grams per serving")] int? fat_g = null,
        [Description("Hands-on minutes")] int? prep_minutes = null,
        [Description("Labels, e.g. \"high-protein\", \"no-cook\", \"post-workout\"")] string[]? tags = null,
        [Description("Star it so it sorts to the front of its slot")] bool is_favorite = false)
    {
        var clean = (name ?? "").Trim();
        if (clean.Length == 0) return J(new { error = "name is required" });
        if (!TryParseSlot(slot, MealSlot.Breakfast, out var slotValue))
            return J(new { error = $"invalid slot '{slot}'. Use {SlotHelp}." });

        var meal = new Meal
        {
            Id = Guid.NewGuid(),
            Slot = slotValue,
            SortOrder = await NextSortOrder(slotValue),
            Name = clean,
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
            Ingredients = CleanList(ingredients),
            Steps = CleanList(steps),
            Calories = NonNegative(calories),
            ProteinG = NonNegative(protein_g),
            CarbsG = NonNegative(carbs_g),
            FatG = NonNegative(fat_g),
            PrepMinutes = NonNegative(prep_minutes),
            Tags = CleanList(tags),
            IsFavorite = is_favorite,
        };
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        return J(new { status = "created", meal = ToDto(meal) });
    }

    [McpServerTool(Name = "update_meal"), Description(
        "Update a saved meal in place. Only the arguments you pass change — anything omitted keeps " +
        "its stored value, so a tweak to one macro cannot wipe the recipe. To clear a list, pass an " +
        "empty array. Moving a meal to another slot puts it at the end of that slot.")]
    public async Task<string> UpdateMeal(
        [Description("Meal UUID")] Guid meal_id,
        [Description("New name")] string? name = null,
        [Description(SlotHelp)] string? slot = null,
        [Description("New one-line rationale")] string? summary = null,
        [Description("Replaces the ingredient list; empty array clears it")] string[]? ingredients = null,
        [Description("Replaces the steps; empty array clears them")] string[]? steps = null,
        [Description("Calories per serving")] int? calories = null,
        [Description("Protein grams per serving")] int? protein_g = null,
        [Description("Carb grams per serving")] int? carbs_g = null,
        [Description("Fat grams per serving")] int? fat_g = null,
        [Description("Hands-on minutes")] int? prep_minutes = null,
        [Description("Replaces the tags; empty array clears them")] string[]? tags = null,
        [Description("Star or unstar it")] bool? is_favorite = null)
    {
        var meal = await db.Meals.FirstOrDefaultAsync(m => m.Id == meal_id);
        if (meal is null) return J(new { error = "meal not found" });

        if (name is not null)
        {
            var clean = name.Trim();
            if (clean.Length == 0) return J(new { error = "name cannot be blank" });
            meal.Name = clean;
        }

        if (!string.IsNullOrWhiteSpace(slot))
        {
            if (!Enum.TryParse<MealSlot>(slot, ignoreCase: true, out var slotValue))
                return J(new { error = $"invalid slot '{slot}'. Use {SlotHelp}." });
            if (slotValue != meal.Slot)
            {
                meal.SortOrder = await NextSortOrder(slotValue);
                meal.Slot = slotValue;
            }
        }

        if (summary is not null) meal.Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        if (ingredients is not null) meal.Ingredients = CleanList(ingredients);
        if (steps is not null) meal.Steps = CleanList(steps);
        if (calories is not null) meal.Calories = NonNegative(calories);
        if (protein_g is not null) meal.ProteinG = NonNegative(protein_g);
        if (carbs_g is not null) meal.CarbsG = NonNegative(carbs_g);
        if (fat_g is not null) meal.FatG = NonNegative(fat_g);
        if (prep_minutes is not null) meal.PrepMinutes = NonNegative(prep_minutes);
        if (tags is not null) meal.Tags = CleanList(tags);
        if (is_favorite is not null) meal.IsFavorite = is_favorite.Value;

        meal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(new { status = "updated", meal = ToDto(meal) });
    }

    [McpServerTool(Name = "delete_meal"), Description("Permanently delete a meal from the meal book.")]
    public async Task<string> DeleteMeal([Description("Meal UUID")] Guid meal_id)
    {
        var meal = await db.Meals.FirstOrDefaultAsync(m => m.Id == meal_id);
        if (meal is null) return J(new { error = "meal not found" });
        db.Meals.Remove(meal);
        await db.SaveChangesAsync();
        return J(new { status = "deleted", name = meal.Name });
    }
}
