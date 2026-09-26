namespace Roadmap.Api.Entities;

/// <summary>
/// A milestone within a course — the unit the stage rail draws. Carries a short human code
/// (<c>M0.5</c>) which is unique per course and is what agents address it by.
/// </summary>
public class Stage
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    /// <summary>Dense within a course, and the order the rail renders.</summary>
    public int Position { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? SummaryMd { get; set; }

    public StageStatus Status { get; set; } = StageStatus.Planned;

    /// <summary>Doubles as the stage's weight in course progress; absent counts as 1.</summary>
    public double? TargetWeeks { get; set; }

    public string Metadata { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public List<Lesson> Lessons { get; set; } = [];
}
