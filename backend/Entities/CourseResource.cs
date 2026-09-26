namespace Roadmap.Api.Entities;

/// <summary>
/// A link worth keeping: the docs page, the paper, the repo. Scoped to the course, or to one
/// lesson when it only matters there. Hard-deleted — it is a title and a URL.
/// </summary>
public class CourseResource
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;

    /// <summary>Null means it belongs to the course as a whole.</summary>
    public Guid? LessonId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    /// <summary>doc | paper | blog | repo | video | book | other — free text, the UI maps what it knows.</summary>
    public string Kind { get; set; } = "other";
    public string? NoteMd { get; set; }
    public int Position { get; set; }
}
