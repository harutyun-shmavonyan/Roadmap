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

    /// <summary>
    /// The planning inputs frozen at "Start Sprint" — item sizes/rates/schedules, block queues,
    /// relax days, the sprint window, and work already logged going in. Serialized
    /// <see cref="Roadmap.Api.Entities.PlanSnapshot"/>.
    ///
    /// This is what makes the sprint's *commitment* stable. The commitment is recomputed from
    /// this snapshot on demand rather than stored, so it can never drift — and because the
    /// snapshot holds the original rates, queue order and relax days, later edits to any of
    /// those leave the commitment untouched. The single exception is an item's size: the
    /// commitment is rebuilt with corrected sizes, which is how "I overestimated this" is
    /// allowed to reshape what you were on the hook for.
    /// </summary>
    public string? PlanInputs { get; set; }
    
    public bool IsOpen => EndDate >= DateOnly.FromDateTime(DateTime.UtcNow);
    
    // Navigation
    public RoadmapDefinition Roadmap { get; set; } = null!;
    public List<SprintPlanEntry> PlanEntries { get; set; } = [];
}
