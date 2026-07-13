namespace Roadmap.Api.Entities;

public enum TaskPriority { High, Medium, Low }

/// <summary>
/// A single-time task (not recurring). Per-roadmap, persists until completed.
/// Shown in the daily schedule alongside regular items based on priority and 3hr cap.
/// Points: 2 per estimated hour, credited on completion day.
/// </summary>
public class SingleTask
{
    public Guid Id { get; set; }
    public Guid RoadmapId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    
    /// <summary>Estimated hours to complete.</summary>
    public double EstimatedHours { get; set; }
    
    /// <summary>Weekdays the task can be scheduled on. JSON array e.g. [1,2,3,4,5].</summary>
    public string? Weekdays { get; set; }
    
    /// <summary>Earliest date the task can appear in the schedule.</summary>
    public DateOnly StartDate { get; set; }
    
    /// <summary>Optional deadline.</summary>
    public DateOnly? DueDate { get; set; }
    
    /// <summary>If delayed, task won't appear until this date.</summary>
    public DateOnly? DelayedUntil { get; set; }
    
    public bool IsCompleted { get; set; }
    public DateOnly? CompletedDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RoadmapDefinition Roadmap { get; set; } = null!;
}
