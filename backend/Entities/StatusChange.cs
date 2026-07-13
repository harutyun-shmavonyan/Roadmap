namespace Roadmap.Api.Entities;

public class StatusChange
{
    public Guid Id { get; set; }
    
    public Guid RoadmapId { get; set; }
    
    public Guid NodeId { get; set; }
    
    public ActionItemStatus OldStatus { get; set; }
    
    public ActionItemStatus NewStatus { get; set; }
    
    /// <summary>
    /// What triggered this change: "manual", "auto_completed", etc.
    /// </summary>
    public string Trigger { get; set; } = "manual";
    
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public RoadmapNode Node { get; set; } = null!;
    public RoadmapDefinition Roadmap { get; set; } = null!;
}
