namespace Roadmap.Api.Entities;

/// <summary>
/// One attempt at an exercise. Append-only: a second try is a second row with the next
/// <see cref="AttemptNo"/>, never an edit, so the record of how the work went is not rewritten by
/// how it ended.
/// </summary>
public class Submission
{
    public Guid Id { get; set; }
    public Guid ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    /// <summary>1-based, dense per exercise, assigned by the service.</summary>
    public int AttemptNo { get; set; }

    /// <summary>The answer, or the agent's summary of work that lives elsewhere.</summary>
    public string? ContentMd { get; set; }

    /// <summary><c>[{label, url, kind}]</c> — where the real work is: a repo, a commit, a PR, a file.</summary>
    public string Links { get; set; } = "[]";

    /// <summary><c>user</c> or <c>agent:&lt;name&gt;</c>.</summary>
    public string SubmittedBy { get; set; } = "user";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set by a caller that may retry; a repeat of the same key returns the first row.</summary>
    public string? IdempotencyKey { get; set; }

    public List<Grade> Grades { get; set; } = [];
}
