namespace Roadmap.Api.Entities;

public class Sprint
{
    public Guid Id { get; set; }
    
    public Guid RoadmapId { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public DateOnly StartDate { get; set; }
    
    public DateOnly EndDate { get; set; }
    
    /// <summary>
    /// Whether the sprint has been started (plan snapshot taken).
    /// </summary>
    public bool IsStarted { get; set; }
    
    public DateTime? StartedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>JSON array of date strings that are relax days, e.g. ["2026-03-28","2026-04-04"]</summary>
    public string? RelaxDays { get; set; }
    
    public bool IsOpen => EndDate >= DateOnly.FromDateTime(DateTime.UtcNow);
    
    // Navigation
    public RoadmapDefinition Roadmap { get; set; } = null!;
    public List<SprintPlanEntry> PlanEntries { get; set; } = [];
}
