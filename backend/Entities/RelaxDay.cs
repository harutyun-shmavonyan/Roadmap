namespace Roadmap.Api.Entities;

/// <summary>
/// Marks a specific date as a relax day. No scheduled items shown, but habits and tasks still appear.
/// </summary>
public class RelaxDay
{
    public Guid Id { get; set; }
    public Guid RoadmapId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public RoadmapDefinition Roadmap { get; set; } = null!;
}
