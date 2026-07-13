namespace Roadmap.Api.Entities;

/// <summary>
/// A custom logged achievement — freetext entry with points, credited to a specific date.
/// Not tied to any scheduled item. Shows in Performance and Week tables.
/// </summary>
public class CustomLog
{
    public Guid Id { get; set; }
    public Guid RoadmapId { get; set; }
    public string Title { get; set; } = string.Empty;
    public double Points { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RoadmapDefinition Roadmap { get; set; } = null!;
}
