namespace Roadmap.Api.Entities;

/// <summary>
/// The photo for a <see cref="Meal"/> — what the plate actually looks like. Optional: a meal is
/// worth keeping with or without a picture, so this is a separate row rather than a column on
/// the meal itself. That keeps the bytes out of every list query, which reads the whole book.
///
/// One image per meal: the meal id is the primary key, so re-uploading replaces what is there
/// and deleting the meal cascades the image away with it.
/// </summary>
public class MealImage
{
    public Guid MealId { get; set; }
    public Meal? Meal { get; set; }

    /// <summary>Original filename, kept only so a re-download has a sensible name.</summary>
    public string? FileName { get; set; }

    /// <summary>MIME type, e.g. "image/jpeg". Always an image/* type — anything else is rejected.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Raw image bytes, stored as Postgres bytea.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>Bumped on every replace — the UI uses it to bust its thumbnail cache.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
