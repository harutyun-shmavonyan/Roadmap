namespace Roadmap.Api.Entities;

/// <summary>
/// The rhythm a course's lessons follow: which sections every lesson has, in what order, and what
/// kinds of exercise can hang off them. This is what keeps the rest of the model course-agnostic —
/// nothing below knows whether it is teaching a framework or a language, only that a lesson has
/// sections whose kinds came from here.
///
/// Both lists are stored as JSON rather than child tables. They are read whole, written whole, and
/// never queried across, which is the same reasoning as the queries array on a job run.
/// </summary>
public class LessonTemplate
{
    public Guid Id { get; set; }

    /// <summary>Unique, and the handle agents use instead of the id (<c>templateName</c>).</summary>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Ordered <c>[{kind, title, required, hasExercises}]</c>. A lesson is Ready once every
    /// required kind is present.</summary>
    public string Sections { get; set; } = "[]";

    /// <summary>
    /// <c>[{kind, label, defaultMaxScore, defaultWeight, sectionKind}]</c> — the exercise kinds this
    /// course grades, and the defaults a new exercise of that kind inherits.
    /// </summary>
    public string ExerciseKinds { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
