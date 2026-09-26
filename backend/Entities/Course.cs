namespace Roadmap.Api.Entities;

/// <summary>
/// A course: durable storage for agent-driven study, and the thing the Courses tab reads. Like
/// articles it is global rather than owned — this app has one user.
///
/// A course is normally created as an outline (stages with titles, lessons as placeholders) and
/// filled in over weeks, so "planned but empty" is the expected state of most of it at any moment,
/// not an error to guard against.
/// </summary>
public class Course
{
    public Guid Id { get; set; }

    /// <summary>Unique, stable, and how agents address the course without holding ids.</summary>
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? DescriptionMd { get; set; }

    /// <summary>The long-running piece of work the course builds towards, if it has one.</summary>
    public string? CapstoneMd { get; set; }

    /// <summary>
    /// The course's own instructions.md: how this course is to be taught, graded and written —
    /// house rules that outlive any one session. It is handed back by <c>get_course_resume</c>, so
    /// the agent picking the course up reads it before it does anything, the way it would read a
    /// CLAUDE.md on entering a repository. Markdown, capped like every other Markdown field.
    /// </summary>
    public string? InstructionsMd { get; set; }

    public Guid TemplateId { get; set; }
    public LessonTemplate Template { get; set; } = null!;

    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public double? TargetHoursPerWeek { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Free JSON. <c>scoring</c> ("best" | "latest") is the one key the app reads.</summary>
    public string Metadata { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public List<Stage> Stages { get; set; } = [];
    public List<CourseResource> Resources { get; set; } = [];
}
