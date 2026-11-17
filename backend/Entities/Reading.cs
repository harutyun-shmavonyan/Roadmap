namespace Roadmap.Api.Entities;

// Evaluations are immutable. A new evaluation key creates a revision, preserving what the
// agent predicted before the reader's feedback. No automatic pruning of learning history.
public class ReadingEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EvaluationKey { get; set; } = "";
    public string ContentKey { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class ReadingEdition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly IssueDate { get; set; }
    public int Revision { get; set; } = 1;
    public string Payload { get; set; } = "";
    public DateTime CoveredUntil { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class ReadingFeedback
{
    public string ContentKey { get; set; } = "";
    public Guid EvaluationId { get; set; }
    public string Payload { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class ReadingSettings
{
    public int Id { get; set; } = 1;
    public string Payload { get; set; } = "";
}
