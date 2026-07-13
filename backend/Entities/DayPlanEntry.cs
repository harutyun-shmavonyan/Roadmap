namespace Roadmap.Api.Entities;

public class DayPlanEntry
{
    public Guid Id { get; set; }
    
    public Guid DayPlanId { get; set; }
    
    /// <summary>
    /// The actionable node this entry is for.
    /// </summary>
    public Guid NodeId { get; set; }
    
    /// <summary>
    /// Start time as minutes from midnight (0–1439).
    /// e.g. 540 = 9:00 AM.
    /// </summary>
    public int StartMinute { get; set; }
    
    /// <summary>
    /// Duration in minutes.
    /// </summary>
    public int DurationMinutes { get; set; } = 60;
    
    /// <summary>
    /// Optional note for this specific time block.
    /// </summary>
    public string? Note { get; set; }
    
    /// <summary>
    /// Actual minutes logged/worked (filled in after the fact).
    /// </summary>
    public int? ActualMinutes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public DayPlan DayPlan { get; set; } = null!;
    public RoadmapNode Node { get; set; } = null!;
}
