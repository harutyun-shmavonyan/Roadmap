namespace Roadmap.Api.Entities;

/// <summary>
/// A pile of single tasks that belong together — "Move flat", "Launch v2". Per roadmap. It carries
/// nothing but a name and a colour: it exists so the Tasks tab can show the work as a few piles
/// instead of one list, and so a task can say which pile it is in. Deleting a project leaves its
/// tasks in place, unassigned (FK ON DELETE SET NULL) — the project is a label, not a container.
/// </summary>
public class TaskProject
{
    public Guid Id { get; set; }
    public Guid RoadmapId { get; set; }

    /// <summary>Unique per roadmap, folded case-insensitively on write by <c>TaskLogic</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// One of <c>TaskLogic.Palette</c> (blue, orange, aqua, yellow, magenta, green, violet, red) —
    /// the key of a validated hue, not a hex, so the tab can map it to the right step for the light
    /// and the dark surface.
    /// </summary>
    public string Color { get; set; } = "blue";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RoadmapDefinition Roadmap { get; set; } = null!;
    public ICollection<SingleTask> Tasks { get; set; } = new List<SingleTask>();
}
