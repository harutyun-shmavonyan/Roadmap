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

/// <summary>
/// One thing occupying the day's calendar. Normally an item — <c>NodeId</c> set, log straight to
/// it. For a pool block it is the *block*: <c>NodeId</c> is null, <c>BlockId</c> and
/// <c>PoolItems</c> are set, the figures are the pool's (average rate, summed progress), and the
/// exact item is chosen from <c>PoolItems</c> when logging.
/// </summary>
public record ScheduleBlockDto(
    Guid? NodeId, string NodeTitle, string NodePath, string? Unit,
    double? UnitsPerHour, double PlannedUnits,
    int StartMinute, int DurationMinutes,
    double TotalLogged, double? TotalSize, double CompletionPercent,
    double? PointsPerUnit,
    bool IsChecklist = false,
    Guid? BlockId = null,
    List<ScheduleBlockOptionDto>? PoolItems = null
);

/// <summary>One candidate inside a pool block's session — what the log picker offers.</summary>
public record ScheduleBlockOptionDto(
    Guid NodeId, string Title, string Path, string? Unit,
    double? TotalSize, double TotalLogged, double? UnitsPerHour, double? PointsPerUnit,
    bool IsChecklist
);

public record SprintDto(Guid Id, string Name, string StartDate, string EndDate, bool IsOpen, bool IsStarted, string? RelaxDays);

public record SprintPlanEntryDto(Guid? NodeId, Guid? BlockId, string NodeTitle, string Date, int StartMinute, int DurationMinutes, double PlannedUnits);

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
    List<SprintGoalDto> SprintGoals,
    /// <summary>Total flat bonus earned for sprint goals reached. Earned, never planned.</summary>
    double GoalBonusPoints = 0
);

/// <summary>
/// Per-item performance within a sprint.
/// PlannedUnits: the sprint's frozen commitment for this item — unmoved by how fast or slow you
///   actually went. Zero for bonus items, which were never committed to.
/// DoneUnits: only work logged within sprint dates (from WorkLogs with matching SprintId).
/// DailyCumulative: day-by-day sprint-local progress — actual and ideal both start at 0%.
/// IsBonus: worked on but never committed to — the queue only reached it because you got ahead.
///   Earns points, owes none, and is kept out of the "completing this sprint" list.
/// </summary>
public record PerformanceItemDto(
    Guid NodeId, string Title, string? Unit,
    double? TotalSize, double? UnitsPerHour, double? PointsPerUnit,
    int ScheduledSessions, double PlannedUnits, double DoneUnits,
    double PlannedPoints, double EarnedPoints,
    double TotalMinutes,
    bool WillComplete, string? ProjectedCompletionDate,
    List<DailyCumulativeDto> DailyCumulative,
    bool IsNodeCompleted,
    bool IsBonus = false,
    /// <summary>
    /// This row is a pool block, not an item: NodeId is the block's id, the planned figures are
    /// the block's commitment, and the done figures are everything its members took. The items
    /// themselves are in <see cref="PoolItems"/> — planned is meaningless per item there, since
    /// the sprint never committed to any one of them.
    /// </summary>
    bool IsPool = false,
    List<PerformanceItemDto>? PoolItems = null
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
// Mode is "Queue" (items take the slot one after another, in order) or "Pool" (the block takes
// the slot; its active items are interchangeable candidates).
public record ScheduleBlockDefDto(Guid Id, string Name, string? ScheduleTemplate, int SortOrder, string Mode, List<ScheduleBlockItemDto> Items);
public record ScheduleBlockItemDto(Guid NodeId, string Title, string? Unit, double? TotalSize, double? UnitsPerHour, string Status, int BlockSortOrder, bool IsActiveInBlock);
public record CreateScheduleBlockRequest(string Name, string? ScheduleTemplate, string? Mode = null);
public record UpdateScheduleBlockRequest(string Name, string? ScheduleTemplate, string? Mode = null);
/// <summary>The full set of items that should be active in a pool block — anything absent goes inactive.</summary>
public record SetBlockItemsActiveRequest(List<Guid> ActiveNodeIds);
public record AssignToBlockRequest(Guid NodeId, int BlockSortOrder);
public record BatchReorderRequest(List<Guid> NodeIds);
public record CategoryTimeDto(string CategoryName, double TotalMinutes, double TotalPoints, int Depth, List<CategoryTimeDto> Children);

// --- Notes (daily notes per book) ---
public record NoteDto(string Book, int DayNumber, string EntryDate, string Content, DateTime CreatedAt, DateTime UpdatedAt);
public record UpdateNoteRequest(string? Content, string? EntryDate);

// --- Articles (global reading library; Markdown or HTML body; marking read earns 3 pts/hour) ---
public record ArticleImageDto(string Name, string ContentType, int SortOrder);
public record ArticleSummaryDto(Guid Id, string Title, string Format, int ReadMinutes, double Points,
    bool IsRead, string? ReadOn, int SortOrder, int ImageCount, DateTime CreatedAt, DateTime UpdatedAt);
public record ArticleDto(Guid Id, string Title, string Format, string Content, int ReadMinutes, double Points,
    bool IsRead, string? ReadOn, int SortOrder, IReadOnlyList<ArticleImageDto> Images, string? ChatUrl, DateTime CreatedAt, DateTime UpdatedAt);
// readMinutes optional on create — auto-estimated from word count (~200 wpm) when omitted.
// format is "markdown" (default) or "html"; HTML bodies reference uploaded images via {{img:NAME}}.
// chatUrl is an optional link back to the conversation that produced the article.
public record CreateArticleRequest(string Title, string? Content, int? ReadMinutes, string? Format, string? ChatUrl);
public record UpdateArticleRequest(string? Title, string? Content, int? ReadMinutes, string? Format, string? ChatUrl);
// roadmapId credits the achievement to a roadmap; date defaults to today (Asia/Yerevan).
public record MarkArticleReadRequest(Guid? RoadmapId, string? Date);

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

// --- Nutrition (meals worth keeping, one row per meal) ---
public record MealDto(Guid Id, string Slot, string Name, string? Summary,
    List<string> Ingredients, List<string> Steps,
    int? Calories, int? ProteinG, int? CarbsG, int? FatG, int? PrepMinutes,
    List<string> Tags, bool IsFavorite, int SortOrder, DateTime CreatedAt, DateTime UpdatedAt);

// Create and update share a shape: the update replaces the whole meal, so a PUT with a
// missing list clears it. Slot defaults to breakfast when omitted.
public record SaveMealRequest(string? Slot, string Name, string? Summary,
    List<string>? Ingredients, List<string>? Steps,
    int? Calories, int? ProteinG, int? CarbsG, int? FatG, int? PrepMinutes,
    List<string>? Tags, bool? IsFavorite);
