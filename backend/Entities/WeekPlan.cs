namespace Roadmap.Api.Entities;

public class WeekPlan
{
    public Guid Id { get; set; }
    
    public Guid RoadmapId { get; set; }
    
    /// <summary>
    /// The Monday of this week.
    /// </summary>
    public DateOnly WeekStart { get; set; }
    
    public bool IsClosed { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public RoadmapDefinition Roadmap { get; set; } = null!;
    public List<WeekPlanGoal> CustomGoals { get; set; } = [];
}

public class WeekPlanGoal
{
    public Guid Id { get; set; }
    
    public Guid WeekPlanId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional target (e.g. "3 PRs", "2 calls").
    /// </summary>
    public string? TargetDescription { get; set; }
    
    /// <summary>
    /// Numeric target amount (optional).
    /// </summary>
    public double? TargetAmount { get; set; }
    
    /// <summary>
    /// Result filled in when closing the week.
    /// </summary>
    public double? ResultAmount { get; set; }
    
    public string? ResultNote { get; set; }
    
    public bool IsCompleted { get; set; }
    
    public int SortOrder { get; set; }
    
    /// <summary>
    /// If linked to a sprint goal, logs roll up to the sprint goal.
    /// </summary>
    public Guid? SprintGoalId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public WeekPlan WeekPlan { get; set; } = null!;
    public SprintGoal? SprintGoal { get; set; }
}
