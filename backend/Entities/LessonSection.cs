namespace Roadmap.Api.Entities;

/// <summary>
/// One part of a lesson, of a kind the course's template declares. At most one section per kind
/// per lesson: several question groups live as several <see cref="Exercise"/> rows inside the one
/// questions section rather than as repeated sections, which is what keeps
/// <c>upsert_lesson_section</c> idempotent on (lesson, kind).
///
/// Hard-deleted rather than soft: it is a slab of Markdown that costs nothing to write again.
/// </summary>
public class LessonSection
{
    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    public string Kind { get; set; } = string.Empty;
    public int Position { get; set; }

    /// <summary>Null falls back to the template's title for this kind.</summary>
    public string? Title { get; set; }
    public string ContentMd { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
