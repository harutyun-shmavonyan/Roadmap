namespace Roadmap.Api.Entities;

/// <summary>Whether an experience is still ahead of you or already had.</summary>
public enum ExperienceStatus
{
    Planned,
    Done,
}

/// <summary>
/// Something worth doing once, or remembering having done: a trip, a concert, a climb, a meal
/// somewhere. The Experiences tab keeps both halves of that — the list of what is planned, and the
/// record of what was had — because they are the same thing at two moments, and an experience
/// moves from one to the other rather than being copied.
///
/// Global rather than roadmap-scoped, like <see cref="Meal"/> and <see cref="Article"/>: this app
/// has one user, and a bucket list is not part of a sprint.
///
/// Almost everything is optional on purpose. A plan starts as a line — "see the northern lights" —
/// long before it has a place, a date or a picture, and it should be worth saving at that point.
/// </summary>
public class Experience
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Free text, not an enum: the categories are whatever the life in question turns out to
    /// contain, and they grow without a migration. Writes are matched case-insensitively against
    /// the ones already in use, so "travel" lands on an existing "Travel" instead of beside it.
    /// </summary>
    public string? Category { get; set; }

    public ExperienceStatus Status { get; set; } = ExperienceStatus.Planned;

    /// <summary>Where, as a person would write it — "Kyoto, Japan", not coordinates.</summary>
    public string? Location { get; set; }

    /// <summary>
    /// When. Both optional — a plan is often undated — and an experience that takes an evening
    /// has a start and no end. When both are set, the end is never before the start.
    /// </summary>
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>The description, in Markdown: what it is, why, what it was like.</summary>
    public string? DescriptionMd { get; set; }

    public List<ExperienceImage> Images { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One picture of an experience — of the place before you go, or of it once you were there. Many
/// per experience, kept in their own table so the bytes stay out of every list query.
/// </summary>
public class ExperienceImage
{
    public Guid Id { get; set; }
    public Guid ExperienceId { get; set; }
    public Experience? Experience { get; set; }

    /// <summary>MIME type, always image/* — anything else is refused at the door.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Raw bytes, as Postgres bytea.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>Original filename, kept only so a re-download has a sensible name.</summary>
    public string? FileName { get; set; }

    /// <summary>A line on what the picture shows. Optional.</summary>
    public string? Caption { get; set; }

    /// <summary>Order in the gallery; the first is the cover.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
