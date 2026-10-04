namespace Roadmap.Api.Entities;

/// <summary>
/// One thing eaten on one day — the Nutrition tab's history, beside the meal book.
///
/// The name and the macros are a <b>snapshot</b> taken when the entry is written, even when it
/// came from the meal book: history must say what was eaten that day, so editing or deleting a
/// meal later never rewrites it. <see cref="MealId"/> only remembers where it came from (and is
/// cleared if that meal is deleted).
///
/// Macros are stored <b>per serving</b>, the same way a meal stores them, and multiplied by
/// <see cref="Servings"/> for the entry's totals — so changing "1 serving" to "1.5" is one field,
/// not four. Every macro is optional: an unknown stays unknown (and the day says it is
/// incomplete) rather than counting as zero.
/// </summary>
public class FoodLogEntry
{
    public Guid Id { get; set; }

    /// <summary>The calendar day it was eaten (Asia/Yerevan, like every other day in the app).</summary>
    public DateOnly Date { get; set; }

    public MealSlot Slot { get; set; } = MealSlot.Snack;

    /// <summary>The meal-book meal it was logged from, if any. Set to null if that meal is deleted.</summary>
    public Guid? MealId { get; set; }

    /// <summary>What was eaten, e.g. "Greek yogurt + berries + walnuts" or "2 eggs on toast".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>How many servings of the macros below. Defaults to 1.</summary>
    public double Servings { get; set; } = 1;

    // --- Macros, per serving. All optional. ---
    public double? Calories { get; set; }
    public double? ProteinG { get; set; }
    public double? CarbsG { get; set; }
    public double? FatG { get; set; }

    /// <summary>Anything worth remembering about it — "half portion", "restaurant, estimated".</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
