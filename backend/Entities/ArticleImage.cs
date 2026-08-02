namespace Roadmap.Api.Entities;

/// <summary>
/// A binary image uploaded for an <see cref="Article"/>. HTML article bodies reference an image
/// by its <see cref="Name"/> using a <c>{{img:NAME}}</c> placeholder; when the standalone HTML
/// document is built the placeholder is replaced with a <c>data:</c> URI carrying these bytes, so
/// the rendered article (and the "open in new tab" view) is fully self-contained.
/// </summary>
public class ArticleImage
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public Article? Article { get; set; }

    /// <summary>Reference name, unique within an article (e.g. "cover.png"). Used in {{img:NAME}}.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>MIME type, e.g. "image/png". Used to build the data: URI.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Raw image bytes, stored as Postgres bytea.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
