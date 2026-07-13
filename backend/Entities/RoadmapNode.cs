namespace Roadmap.Api.Entities;

public class RoadmapNode
{
    public Guid Id { get; set; }
    
    public Guid RoadmapId { get; set; }
    
    public Guid? ParentId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    
    public bool IsActionable { get; set; }

    /// <summary>
    /// Boolean checklist item: no quantitative tracking (no Unit/TotalSize/UnitsPerHour),
    /// no schedule. Auto-completes when all actionable children are Completed.
    /// </summary>
    public bool IsChecklist { get; set; }

    public ActionItemStatus Status { get; set; } = ActionItemStatus.NotStarted;
    
    /// <summary>
    /// The unit of measurement for tracking (e.g. "pages", "hours", "sessions", "problems").
    /// Only meaningful when IsActionable = true.
    /// </summary>
    public string? Unit { get; set; }
    
    /// <summary>
    /// Total size / target amount in the given unit (e.g. 350 pages, 40 hours).
    /// </summary>
    public double? TotalSize { get; set; }
    
    /// <summary>
    /// How many units are produced per hour of work (e.g. 30 pages/hour).
    /// Used to calculate planned amounts: (durationMinutes / 60) * UnitsPerHour.
    /// </summary>
    public double? UnitsPerHour { get; set; }
    
    /// <summary>
    /// Points earned per unit completed (e.g. 0.5 pts/page, 10 pts/session).
    /// </summary>
    public double? PointsPerUnit { get; set; }
    
    /// <summary>
    /// Weekly schedule template — JSON: { "days": [1,2,3,5], "startMinute": 540, "durationMinutes": 60 }
    /// Days: 0=Sun, 1=Mon ... 6=Sat.
    /// </summary>
    public string? ScheduleTemplate { get; set; }
    
    /// <summary>Optional: if assigned to a schedule block, the item inherits the block's schedule and queues sequentially.</summary>
    public Guid? ScheduleBlockId { get; set; }
    
    /// <summary>Sort order within the schedule block queue.</summary>
    public int BlockSortOrder { get; set; }
    
    public int SortOrder { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public RoadmapNode? Parent { get; set; }
    public List<RoadmapNode> Children { get; set; } = [];
    public RoadmapDefinition Roadmap { get; set; } = null!;
    public ScheduleBlock? ScheduleBlock { get; set; }
    public List<DayPlanEntry> DayPlanEntries { get; set; } = [];
    public List<WorkLog> WorkLogs { get; set; } = [];
    
    /// <summary>
    /// Additional category links (many-to-many). The primary parent is still ParentId.
    /// </summary>
    public List<NodeCategoryLink> CategoryLinks { get; set; } = [];
    public List<StatusChange> StatusChanges { get; set; } = [];
}
