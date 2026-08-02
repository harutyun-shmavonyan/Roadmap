namespace Roadmap.Api.Entities;

/// <summary>
/// Everything the planner needed to produce the sprint's opening plan, frozen at Start Sprint
/// and stored as JSON on <see cref="Sprint.PlanInputs"/>.
///
/// The sprint's *commitment* is recomputed from this on demand. Because the snapshot carries
/// the original rates, schedules, queue order, relax days and window, none of those can move
/// the commitment after the fact — only an item's corrected size can, which is the one
/// exception the scoring model allows.
/// </summary>
public sealed record PlanSnapshot(
    string StartDate,
    string EndDate,
    List<string> RelaxDays,
    List<SnapshotBlock> Blocks,
    List<SnapshotNode> Nodes,
    Dictionary<string, double> LoggedBefore
);

/// <summary>A schedule block and its queue order as they stood at Start Sprint.</summary>
public sealed record SnapshotBlock(Guid Id, string? ScheduleTemplate, List<Guid> ItemIds);

/// <summary>
/// One actionable item's planning inputs at Start Sprint. <c>TotalSize</c> is the estimate that
/// was made then; it is deliberately re-read from the live item when the commitment is rebuilt.
/// </summary>
public sealed record SnapshotNode(
    Guid Id,
    double? TotalSize,
    double? UnitsPerHour,
    double? PointsPerUnit,
    string? ScheduleTemplate,
    Guid? BlockId,
    int BlockSortOrder,
    int SortOrder,
    ActionItemStatus Status
);
