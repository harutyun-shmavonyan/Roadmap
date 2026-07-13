namespace Roadmap.Api.Entities;

/// <summary>
/// A checklist item attached to a checklist-type RoadmapNode. Templates are static (same every day).
/// </summary>
public class NodeSubPoint
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public RoadmapNode Node { get; set; } = null!;
    public List<NodeSubPointCheck> Checks { get; set; } = [];
}

/// <summary>
/// Per-day check state for a subpoint. When all of a node's subpoints have a check for a given date,
/// the schedule view auto-logs 1 unit for that node/date.
/// </summary>
public class NodeSubPointCheck
{
    public Guid Id { get; set; }
    public Guid SubPointId { get; set; }
    public DateOnly Date { get; set; }

    public NodeSubPoint SubPoint { get; set; } = null!;
}
