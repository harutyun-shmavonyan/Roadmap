namespace Roadmap.Api.Entities;

/// <summary>
/// A snapshot entry: on this date, this item was planned for this time slot.
/// Created when "Start Sprint" is clicked. Immutable after that.
/// </summary>
public class SprintPlanEntry
{
    public Guid Id { get; set; }
    
    public Guid SprintId { get; set; }
    
    public Guid NodeId { get; set; }
    
    /// <summary>
    /// The category that owns this time slot (null if item has its own schedule).
    /// </summary>
    public Guid? CategoryId { get; set; }
    
    public DateOnly Date { get; set; }
    
    public int StartMinute { get; set; }
    
    public int DurationMinutes { get; set; }
    
    /// <summary>
    /// How many units were planned for this session at snapshot time.
    /// </summary>
    public double PlannedUnits { get; set; }
    
    // Navigation
    public Sprint Sprint { get; set; } = null!;
    public RoadmapNode Node { get; set; } = null!;
}
