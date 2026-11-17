namespace Roadmap.Api.Reading;

public record Score(int? Value, string Reason);
public record Source(string Title, string Url);
public record EvaluationInput(
    string EvaluationKey, string Url, string Title, string Publisher, string Author,
    string Access, string Decision, string Section, string Format,
    string WhyRead, string LearningGain, string Summary, string OriginalValue,
    string Durability, string Uncertainty, int ReadMinutes,
    Dictionary<string, Score> Scores, List<string> Topics, List<Source> Sources,
    string? PublishedDate = null, string? ImageUrl = null, string? ImageCredit = null,
    string? ExplorationKind = null, string? Thinker = null, string? Model = null,
    string RubricVersion = "1");
public record EditionInput(string IssueDate, string Title, DateTimeOffset CoveredFrom,
    DateTimeOffset CoveredUntil, List<Guid> EvaluationIds, string EditorNote,
    int? ExpectedRevision = null);
public record FeedbackInput(string? WorthTime = null, string? Understanding = null,
    string? WorthDiscovering = null, string? Direction = null, string? Note = null);
public record SettingsInput(int DailyMinutes = 30, int ExplorationPercent = 25,
    string Preferences = "Select material that adds substantial understanding, explains consequential developments, or introduces unfamiliar perspectives and distinctive thinkers. Prioritize substance, evidence, explanatory depth, lasting insight and value per minute. Recommend originals only when full reading adds value beyond a good summary; consolidate other useful findings. Preserve broad exploration without defaulting to the reader's location, profession or existing interests.");
public record ReadInput(bool IsRead, int ExpectedRevision);
