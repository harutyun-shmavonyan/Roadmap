namespace Roadmap.Api.Entities;

/// <summary>
/// One sitting's worth of a course. Its <see cref="Code"/> (<c>0.5.2</c>) is unique across the
/// course, not just the stage, so a lesson keeps its name when it is moved between stages.
///
/// A <see cref="LessonStatus.Placeholder"/> lesson has no sections and no exercises: it is a
/// promise that this lesson will exist. That is why nothing here is required.
/// </summary>
public class Lesson
{
    public Guid Id { get; set; }
    public Guid StageId { get; set; }
    public Stage Stage { get; set; } = null!;

    public int Position { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? SummaryMd { get; set; }

    public LessonStatus Status { get; set; } = LessonStatus.Placeholder;
    public double? EstimatedHours { get; set; }

    public string Metadata { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public List<LessonSection> Sections { get; set; } = [];
    public List<Exercise> Exercises { get; set; } = [];
}
