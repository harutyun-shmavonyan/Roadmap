namespace Roadmap.Api.Entities;

/// <summary>
/// A habit in the global library. Can be linked to multiple sprints.
/// </summary>
public class Habit
{
    public Guid Id { get; set; }
    public Guid RoadmapId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RoadmapDefinition Roadmap { get; set; } = null!;
    public List<SprintHabit> SprintHabits { get; set; } = [];
}

/// <summary>
/// Links a habit to a sprint for tracking.
/// </summary>
public class SprintHabit
{
    public Guid Id { get; set; }
    public Guid SprintId { get; set; }
    public Guid HabitId { get; set; }
    public bool IsPaused { get; set; }

    // Navigation
    public Sprint Sprint { get; set; } = null!;
    public Habit Habit { get; set; } = null!;
    public List<HabitCheck> Checks { get; set; } = [];
}

/// <summary>
/// Daily habit check within a sprint.
/// </summary>
public class HabitCheck
{
    public Guid Id { get; set; }
    public Guid SprintHabitId { get; set; }
    public DateOnly Date { get; set; }
    public bool IsChecked { get; set; }

    // Navigation
    public SprintHabit SprintHabit { get; set; } = null!;
}
