namespace Roadmap.Api.Entities;

/// <summary>
/// Something to be attempted and graded. Kinds, default scores and default weights come from the
/// course template, so adding a new kind of work to a course is data rather than code.
///
/// <see cref="ReferenceMd"/> is what a strong answer contains — kept beside the prompt so the
/// agent grading a submission has the yardstick without the owner having to restate it.
/// </summary>
public class Exercise
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    /// <summary>Which section of the lesson it renders under; denormalized from the template.</summary>
    public string SectionKind { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PromptMd { get; set; } = string.Empty;
    public string? ReferenceMd { get; set; }

    public double MaxScore { get; set; } = 10;

    /// <summary>Its share of the lesson score; a build counts for more than a comprehension.</summary>
    public double Weight { get; set; } = 1;

    /// <summary>Optional exercises are shown and gradeable but never block a lesson completing.</summary>
    public bool Required { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public List<Submission> Submissions { get; set; } = [];
}
