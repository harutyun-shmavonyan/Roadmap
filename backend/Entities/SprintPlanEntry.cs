namespace Roadmap.Api.Entities;

/// <summary>
/// A plan entry: on this date, this item is planned for this time slot.
///
/// First written when "Start Sprint" is clicked. From then on the plan is *living*: entries
/// dated before today are frozen history and are never rewritten, while entries from today
/// onward are rebuilt by <c>RoadmapEndpoints.ReplanStartedSprintsAsync</c> whenever something
/// that shapes the plan changes mid-sprint — work logged (including backdated), an item
/// completed or reopened, a size/rate/schedule edit, or a queue reorder. That is what lets an
/// item finished early hand its remaining sessions to whatever is queued behind it.
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
