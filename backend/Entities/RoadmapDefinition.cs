namespace Roadmap.Api.Entities;

public class RoadmapDefinition
{
    public Guid Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public List<RoadmapNode> Nodes { get; set; } = [];
}
