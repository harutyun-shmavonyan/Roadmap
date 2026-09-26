namespace Roadmap.Api.Entities;

/// <summary>
/// The course's audit trail and its memory between sessions. Append-only, written by the service
/// layer on every mutation rather than by callers, so there is exactly one place where "did that
/// get recorded" can be answered.
///
/// The <see cref="ProgressEventType.Handoff"/> entry is the one a human reads: what the last
/// session stopped in the middle of, which is what <c>get_course_resume</c> hands back first.
/// </summary>
public class ProgressEvent
{
    public long Id { get; set; }
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    public Guid? StageId { get; set; }
    public Guid? LessonId { get; set; }
    public Guid? ExerciseId { get; set; }

    public ProgressEventType Type { get; set; }

    /// <summary>Shape depends on the type: <c>{from,to}</c>, <c>{minutes}</c>, <c>{md}</c>, <c>{summary}</c>.</summary>
    public string Payload { get; set; } = "{}";

    /// <summary><c>user</c>, <c>agent:&lt;name&gt;</c>, or <c>system</c> for auto-transitions.</summary>
    public string Actor { get; set; } = "user";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
