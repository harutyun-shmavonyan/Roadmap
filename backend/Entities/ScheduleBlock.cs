namespace Roadmap.Api.Entities;

/// <summary>
/// A named time slot that items can be assigned to.
/// The block defines the schedule (days, start time, duration); <see cref="Mode"/> decides what
/// actually occupies it — the head of the queue, or the block itself with its items as a pool.
/// </summary>
public class ScheduleBlock
{
    public Guid Id { get; set; }
    public Guid RoadmapId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Queue (one item at a time, in order) or Pool (the block is scheduled).</summary>
    public ScheduleBlockMode Mode { get; set; } = ScheduleBlockMode.Queue;

    /// <summary>Schedule template JSON — same format as node templates.</summary>
    public string? ScheduleTemplate { get; set; }
    
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RoadmapDefinition Roadmap { get; set; } = null!;
    public List<RoadmapNode> Items { get; set; } = [];
}
