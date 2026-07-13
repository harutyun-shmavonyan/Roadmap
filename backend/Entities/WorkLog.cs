namespace Roadmap.Api.Entities;

/// <summary>
/// Records the amount of work done on a specific action item on a specific day within a sprint.
/// Amount is in the item's unit (pages read, hours practiced, etc.).
/// </summary>
public class WorkLog
{
    public Guid Id { get; set; }
    
    public Guid RoadmapId { get; set; }
    
    public Guid SprintId { get; set; }
    
    public Guid NodeId { get; set; }
    
    public DateOnly Date { get; set; }
    
    /// <summary>
    /// Amount of work in the action item's unit.
    /// </summary>
    public double Amount { get; set; }
    
    public string? Note { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public RoadmapNode Node { get; set; } = null!;
    public RoadmapDefinition Roadmap { get; set; } = null!;
    public Sprint Sprint { get; set; } = null!;
}
