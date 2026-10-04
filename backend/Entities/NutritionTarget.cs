namespace Roadmap.Api.Entities;

/// <summary>
/// The daily targets the food log is read against: maintenance calories, and optionally protein,
/// carbs and fat. Each row takes effect on <see cref="EffectiveFrom"/> and holds until the next
/// one, so a changed maintenance (a new weight, a new training block) never re-judges the days
/// that were lived against the old one. At most one row per date. Every figure is optional.
/// </summary>
public class NutritionTarget
{
    public Guid Id { get; set; }

    /// <summary>The first day these targets apply (Asia/Yerevan).</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Maintenance calories — what a day costs, so a day above it is a surplus.</summary>
    public double? Calories { get; set; }
    public double? ProteinG { get; set; }
    public double? CarbsG { get; set; }
    public double? FatG { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
