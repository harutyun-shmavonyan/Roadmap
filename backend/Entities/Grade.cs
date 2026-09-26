namespace Roadmap.Api.Entities;

/// <summary>
/// A mark against one submission. Regrading does not overwrite: it writes a new row pointing at
/// the one it replaces through <see cref="SupersedesGradeId"/>, so a change of mind stays legible
/// and the lesson score can be recomputed from whichever grade the course's scoring rule prefers.
/// </summary>
public class Grade
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public double Score { get; set; }
    public double MaxScore { get; set; } = 10;
    public string? FeedbackMd { get; set; }

    /// <summary>Optional <c>[{criterion, score, max, note}]</c> breakdown behind the number.</summary>
    public string? Rubric { get; set; }

    /// <summary><c>user</c> or <c>agent:&lt;name&gt;</c>.</summary>
    public string GradedBy { get; set; } = "user";
    public DateTime GradedAt { get; set; } = DateTime.UtcNow;

    public Guid? SupersedesGradeId { get; set; }
    public string? IdempotencyKey { get; set; }
}
