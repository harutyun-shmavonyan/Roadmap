namespace Roadmap.Api.Entities;

public class DayPlan
{
    public Guid Id { get; set; }
    
    public Guid RoadmapId { get; set; }
    
    /// <summary>
    /// The calendar date this plan represents (date only, no time).
    /// </summary>
    public DateOnly Date { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public RoadmapDefinition Roadmap { get; set; } = null!;
    public List<DayPlanEntry> Entries { get; set; } = [];
}
