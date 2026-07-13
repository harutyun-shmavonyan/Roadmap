namespace Roadmap.Api.Entities;

/// <summary>
/// A named time slot that items can be assigned to.
/// Items in a block queue sequentially — when one finishes, the next starts.
/// The block defines the schedule (days, start time, duration).
/// </summary>
public class ScheduleBlock
{
    public Guid Id { get; set; }
    public Guid RoadmapId { get; set; }
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Schedule template JSON — same format as node templates.</summary>
    public string? ScheduleTemplate { get; set; }
    
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RoadmapDefinition Roadmap { get; set; } = null!;
    public List<RoadmapNode> Items { get; set; } = [];
}
