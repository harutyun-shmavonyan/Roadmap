namespace Roadmap.Api.Entities;

/// <summary>Which part of the day a meal belongs to.</summary>
public enum MealSlot
{
    Breakfast,
    Lunch,
    Dinner,
    Snack,
}

/// <summary>
/// One meal worth eating — a keeper, not a log entry. The Nutrition tab is a personal
/// cookbook of what to eat for a good body: what goes in it, how it's made, and what it
/// gives you macro-wise.
///
/// Meals are global (not scoped to a roadmap), like <see cref="Note"/> and
/// <see cref="VocabEntry"/>. Every macro field is optional so a meal can be saved the
/// moment it's worth remembering and have its numbers filled in later.
/// </summary>
public class Meal
{
    public Guid Id { get; set; }

    public MealSlot Slot { get; set; } = MealSlot.Breakfast;

    /// <summary>What it's called, e.g. "Greek yogurt + berries + walnuts".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One line on why it earns its place — the "good body" rationale.</summary>
    public string? Summary { get; set; }

    /// <summary>What goes in it, one line per ingredient (with the amount).</summary>
    public List<string> Ingredients { get; set; } = [];

    /// <summary>How it's made, one line per step. Optional — many breakfasts are assembly, not cooking.</summary>
    public List<string> Steps { get; set; } = [];

    // --- Macros, per serving. All optional. ---
    public int? Calories { get; set; }
    public int? ProteinG { get; set; }
    public int? CarbsG { get; set; }
    public int? FatG { get; set; }

    /// <summary>Hands-on time in minutes.</summary>
    public int? PrepMinutes { get; set; }

    /// <summary>Free-form labels, e.g. "high-protein", "no-cook", "post-workout".</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Starred meals sort to the front of their slot.</summary>
    public bool IsFavorite { get; set; }

    /// <summary>Manual ordering within a slot; new meals go to the end.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
