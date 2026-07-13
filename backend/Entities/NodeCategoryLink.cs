namespace Roadmap.Api.Entities;

/// <summary>
/// Links an actionable node to additional category nodes (beyond its primary parent).
/// </summary>
public class NodeCategoryLink
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// The actionable (leaf) node.
    /// </summary>
    public Guid NodeId { get; set; }
    
    /// <summary>
    /// The category (branch) node this item also belongs to.
    /// </summary>
    public Guid CategoryId { get; set; }
    
    // Navigation
    public RoadmapNode Node { get; set; } = null!;
    public RoadmapNode Category { get; set; } = null!;
}
