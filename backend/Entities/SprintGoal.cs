namespace Roadmap.Api.Entities;

public class SprintGoal
{
    public Guid Id { get; set; }
    public Guid SprintId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public double TargetAmount { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Sprint Sprint { get; set; } = null!;
    public List<SprintGoalLog> Logs { get; set; } = [];
}

public class SprintGoalLog
{
    public Guid Id { get; set; }
    public Guid SprintGoalId { get; set; }
    public DateOnly Date { get; set; }
    public double Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SprintGoal SprintGoal { get; set; } = null!;
}
