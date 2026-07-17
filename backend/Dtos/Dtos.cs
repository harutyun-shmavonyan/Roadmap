namespace Roadmap.Api.Dtos;

// --- Responses ---

public record RoadmapSummaryDto(Guid Id, string Name, string? Description, DateTime CreatedAt);
public record RoadmapTreeDto(Guid Id, string Name, string? Description, List<NodeDto> Roots);

public record NodeDto(
    Guid Id, Guid? ParentId, string Title, bool IsActionable, string Status,
    string? Unit, double? TotalSize, double? UnitsPerHour, double? PointsPerUnit,
    string? ScheduleTemplate, int SortOrder,
    Guid? ScheduleBlockId, int BlockSortOrder,
    List<CategoryLinkDto> CategoryLinks, List<NodeDto> Children,
    bool IsChecklist = false
);

public record CategoryLinkDto(Guid LinkId, Guid CategoryId, string CategoryTitle);

// --- Node SubPoints (checklist subpoints attached to a node) ---
public record NodeSubPointDto(Guid Id, string Title, int SortOrder);
public record ScheduleSubPointDto(Guid Id, string Title, int SortOrder, bool IsChecked);
public record CreateNodeSubPointRequest(string Title);
public record UpdateNodeSubPointRequest(string Title);
public record ToggleNodeSubPointRequest(bool IsChecked);

public record DayPlanDto(
    Guid Id, Guid RoadmapId, string Date, string? Notes,
    List<DayPlanEntryDto> Entries, SprintDto? ActiveSprint
);

public record DayPlanEntryDto(
    Guid Id, Guid NodeId, string NodeTitle, string NodePath, string? Unit,
    int StartMinute, int DurationMinutes, string? Note, int? ActualMinutes
);

public record ActionableItemDto(
    Guid Id, string Title, string Path, string Status,
    string? Unit, double? TotalSize, double? UnitsPerHour, double? PointsPerUnit,
    double TotalLogged, string? ScheduleTemplate
);

public record ScheduleBlockDto(
    Guid NodeId, string NodeTitle, string NodePath, string? Unit,
    double? UnitsPerHour, double PlannedUnits,
    int StartMinute, int DurationMinutes,
    double TotalLogged, double? TotalSize, double CompletionPercent,
    double? PointsPerUnit,
    bool IsChecklist = false
);

public record SprintDto(Guid Id, string Name, string StartDate, string EndDate, bool IsOpen, bool IsStarted, string? RelaxDays);

public record SprintPlanEntryDto(Guid NodeId, string NodeTitle, string Date, int StartMinute, int DurationMinutes, double PlannedUnits);

public record WorkLogDto(Guid Id, Guid NodeId, string NodeTitle, string Date, double Amount, string? Unit, string? Note);

/// <summary>Full log history for a single item.</summary>
public record WorkLogHistoryDto(Guid NodeId, string NodeTitle, string? Unit, List<WorkLogHistoryEntryDto> Entries);
public record WorkLogHistoryEntryDto(Guid Id, string Date, double Amount, string? Note, string? SprintName);

public record StatusChangeDto(Guid Id, Guid NodeId, string NodeTitle, string OldStatus, string NewStatus, string Trigger, string ChangedAt);

// --- Performance (sprint-scoped, all based on sprint plan + sprint work logs) ---

public record PerformanceSummaryDto(
    List<PerformanceItemDto> Items,
    double TotalPlannedPoints,
    double TotalEarnedPoints,
    List<DailyPointsDto> DailyPoints,
    List<CompletedTaskDto> CompletedTasks,
    List<CustomLogDto> CustomLogs,
    List<CategoryTimeDto> CategoryBreakdown,
    List<SprintGoalDto> SprintGoals
);

/// <summary>
/// Per-item performance within a sprint.
/// PlannedUnits: capped at remaining for queued items, sessions may shift to next item.
/// DoneUnits: only work logged within sprint dates (from WorkLogs with matching SprintId).
/// DailyCumulative: day-by-day sprint-local progress — actual and ideal both start at 0%.
/// </summary>
public record PerformanceItemDto(
    Guid NodeId, string Title, string? Unit,
    double? TotalSize, double? UnitsPerHour, double? PointsPerUnit,
    int ScheduledSessions, double PlannedUnits, double DoneUnits,
    double PlannedPoints, double EarnedPoints,
    double TotalMinutes,
    bool WillComplete, string? ProjectedCompletionDate,
    List<DailyCumulativeDto> DailyCumulative,
    bool IsNodeCompleted
);

/// <summary>
/// CumulativePercent = cumulative done / total planned for this item in the sprint (0-100 scale).
/// IdealPercent = cumulative planned / total planned for this item in the sprint (0-100 scale).
/// Both start at 0 on day 1.
/// </summary>
public record DailyCumulativeDto(string Date, double CumulativePercent, double IdealPercent);

public record DailyPointsDto(string Date, double Points);

// --- Requests ---

public record CreateRoadmapRequest(string Name, string? Description);

public record CreateNodeRequest(Guid? ParentId, string Title, bool IsActionable, int SortOrder = 0,
    string? Unit = null, double? TotalSize = null, double? UnitsPerHour = null,
    double? PointsPerUnit = null, string? ScheduleTemplate = null, bool IsChecklist = false);

public record UpdateNodeRequest(string Title, bool IsActionable, int SortOrder,
    string? Unit = null, double? TotalSize = null, double? UnitsPerHour = null,
    double? PointsPerUnit = null, string? ScheduleTemplate = null, bool IsChecklist = false);

public record UpdateNodeStatusRequest(string Status);
public record MoveNodeRequest(Guid? NewParentId, int SortOrder);
public record ReorderNodeRequest(string Direction);
public record AddCategoryLinkRequest(Guid CategoryId);
public record CreateDayPlanEntryRequest(Guid NodeId, int StartMinute, int DurationMinutes = 60, string? Note = null);
public record UpdateDayPlanEntryRequest(int StartMinute, int DurationMinutes, string? Note, int? ActualMinutes);
public record UpdateDayPlanNotesRequest(string? Notes);
public record CreateSprintRequest(string Name, string StartDate, string EndDate);
public record UpdateSprintRequest(string Name, string StartDate, string EndDate);
public record LogWorkRequest(Guid NodeId, string Date, double Amount, string? Note = null);
public record UpdateWorkLogRequest(double Amount, string? Note);

// --- Week Plan (sprint-scoped) ---
public record WeekPlanDto(
    Guid Id, Guid RoadmapId, string WeekStart, bool IsClosed, string? Notes,
    List<WeekScheduledItemDto> ScheduledItems,
    List<WeekPlanGoalDto> CustomGoals,
    List<CompletedTaskDto> CompletedTasks,
    List<CustomLogDto> CustomLogs,
    SprintDto ActiveSprint,
    List<SprintGoalDto> SprintGoals
);

public record CompletedTaskDto(Guid Id, string Title, string Priority, double EstimatedHours, double Points, string CompletedDate);

public record WeekScheduledItemDto(
    Guid NodeId, string Title, string? Unit, double? UnitsPerHour,
    int SessionsThisWeek, double PlannedUnits, double LoggedUnits,
    double? TotalSize, double TotalLogged, bool WillCompleteThisSprint, string? ProjectedCompletionDate,
    bool IsNodeCompleted = false
);

public record WeekPlanGoalDto(
    Guid Id, string Title, string? TargetDescription,
    double? TargetAmount, double? ResultAmount, string? ResultNote,
    bool IsCompleted, int SortOrder,
    Guid? SprintGoalId, string? SprintGoalTitle, double? SprintGoalTarget, double? SprintGoalLogged, string? SprintGoalUnit
);

public record CreateWeekPlanGoalRequest(string Title, string? TargetDescription = null, double? TargetAmount = null, Guid? SprintGoalId = null);
public record UpdateWeekPlanGoalRequest(string Title, string? TargetDescription, double? TargetAmount, double? ResultAmount, string? ResultNote, bool IsCompleted);
public record CloseWeekPlanRequest(string? Notes);

// --- Habits ---
public record HabitDto(Guid Id, string Name, DateTime CreatedAt);
public record SprintHabitDto(Guid SprintHabitId, Guid HabitId, string Name, bool IsPaused, int CurrentStreak, int BestStreak, bool IsFormed, List<HabitCheckDto> Checks);
public record HabitCheckDto(string Date, bool IsChecked);
public record CreateHabitRequest(string Name);
public record AddSprintHabitRequest(Guid HabitId);
public record ToggleHabitCheckRequest(string Date, bool IsChecked);

/// <summary>Schedule-page view: habits for a specific date within a sprint.</summary>
public record ScheduleHabitDto(Guid SprintHabitId, Guid HabitId, string Name, bool IsCheckedToday, int CurrentStreak, bool IsFormed);

// --- Single Tasks ---
public record SingleTaskDto(Guid Id, string Title, string Priority, double EstimatedHours, string? Weekdays,
    string StartDate, string? DueDate, string? DelayedUntil, bool IsCompleted, string? CompletedDate, double Points);
public record CreateSingleTaskRequest(string Title, string Priority, double EstimatedHours, string? Weekdays, string StartDate, string? DueDate);
public record UpdateSingleTaskRequest(string Title, string Priority, double EstimatedHours, string? Weekdays, string StartDate, string? DueDate);
public record ScheduleTaskDto(Guid Id, string Title, string Priority, double EstimatedHours, double Points, bool IsCompleted, string? DueDate, bool IsOverdue);
public record CompleteTaskRequest(string Date);

// --- Custom Logs ---
public record CustomLogDto(Guid Id, string Title, double Points, string Date, string? Note);
public record CreateCustomLogRequest(string Title, double Points, string Date, string? Note);

// --- Schedule Blocks ---
public record ScheduleBlockDefDto(Guid Id, string Name, string? ScheduleTemplate, int SortOrder, List<ScheduleBlockItemDto> Items);
public record ScheduleBlockItemDto(Guid NodeId, string Title, string? Unit, double? TotalSize, double? UnitsPerHour, string Status, int BlockSortOrder);
public record CreateScheduleBlockRequest(string Name, string? ScheduleTemplate);
public record UpdateScheduleBlockRequest(string Name, string? ScheduleTemplate);
public record AssignToBlockRequest(Guid NodeId, int BlockSortOrder);
public record BatchReorderRequest(List<Guid> NodeIds);
public record CategoryTimeDto(string CategoryName, double TotalMinutes, double TotalPoints, int Depth, List<CategoryTimeDto> Children);

// --- Notes (daily notes per book) ---
public record NoteDto(string Book, int DayNumber, string EntryDate, string Content, DateTime CreatedAt, DateTime UpdatedAt);
public record UpdateNoteRequest(string? Content, string? EntryDate);

// --- Sprint Goals ---
public record SprintGoalDto(Guid Id, string Title, string? Unit, double TargetAmount, string? Description, int SortOrder, double LoggedAmount);
public record CreateSprintGoalRequest(string Title, string? Unit, double TargetAmount, string? Description);
public record UpdateSprintGoalRequest(string Title, string? Unit, double TargetAmount, string? Description);
public record LogSprintGoalRequest(string Date, double Amount);

// --- English vocabulary (words / idioms / phrasal verbs, SM-2 scheduled) ---
public record VocabReviewDto(DateTime ReviewedAt, int Grade, string? PromptType, string? Answer, string? Note,
    int IntervalBefore, int IntervalAfter, double EaseBefore, double EaseAfter);

public record VocabEntryDto(Guid Id, string Term, string Kind, string Definition, string? GlossHy, string? GlossRu,
    string Frequency, string Register, List<string> Examples, List<string> Collocations, List<string> Synonyms,
    string? MemoryHook, string? SourceContext, string? Notes,
    int Repetitions, double EaseFactor, int IntervalDays, string DueOn, DateTime? LastReviewedAt,
    int Lapses, int TotalReviews, string Strength, bool IsDue, DateTime CreatedAt,
    List<VocabReviewDto> Reviews);

public record VocabStatsDto(int Total, int DueToday, int New, int Learning, int Young, int Mature,
    int ReviewsAllTime, int ReviewsLast7Days, double AverageEase, int Lapses);

// --- Job scouting (postings imported from the Finder pipeline, one run per day) ---
// One gap keeping the tailored CV below a perfect fit. Points = how much closing it adds toward 100.
public record CvFitGapDto(string Label, int Points, string? Note);

// The gap breakdown is stored as raw JSON text on the posting. Parse it leniently into the
// typed DTO list (sorted highest-impact first), so bad/empty JSON is just "no gaps", never a 500.
public static class CvFitGapsJson
{
    private static readonly System.Text.Json.JsonSerializerOptions Opts =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    public static List<CvFitGapDto> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var gaps = System.Text.Json.JsonSerializer.Deserialize<List<CvFitGapDto>>(json, Opts);
            return gaps is null
                ? []
                : [.. gaps.Where(g => !string.IsNullOrWhiteSpace(g.Label)).OrderByDescending(g => g.Points)];
        }
        catch (System.Text.Json.JsonException) { return []; }
    }
}

public record JobPostingDto(Guid Id, string Title, string Company, string Url, string Source,
    string? Location, string? PostedAt, string Description, string Bucket,
    string? SeniorityClass, int AiKeywordHits, List<string> GeoHints, List<string> Queries,
    double? Score, string? Reasoning, int SortOrder,
    // HasCv drives the "Download CV" button; the PDF bytes are served separately
    // (GET /api/job-runs/postings/{id}/cv) so they never bloat the list JSON.
    bool HasCv, string? CvChangeList,
    // CV-vs-JD fit score (0–100) and the gap breakdown (highest-impact first).
    int? CvFitScore, List<CvFitGapDto> CvFitGaps);

public record JobRunSummaryDto(Guid Id, string RunDate, List<string> Queries, int MaxAgeDays,
    int RawCount, int PostingCount, DateTime CreatedAt);

public record JobRunDto(Guid Id, string RunDate, List<string> Queries, int MaxAgeDays,
    int RawCount, DateTime CreatedAt, List<JobPostingDto> Postings);
