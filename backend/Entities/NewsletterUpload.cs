namespace Roadmap.Api.Entities;

/// <summary>
/// An edition being uploaded in parts. One <c>publish_newsletter</c> call carries the whole
/// document in a single tool call, and an agent cannot write much more than ~135 KB into one;
/// an upload lifts that by taking the document a part at a time. Nothing is published until the
/// upload is finished — then the parts are joined and go through the same publish path — so a run
/// that dies halfway leaves the day's edition as it was rather than half a page. The parts live in
/// the database between calls so a restart in the middle loses nothing; uploads never finished are
/// cleared after <c>NewsletterLogic.UploadExpiryHours</c>.
/// </summary>
public class NewsletterUpload
{
    public Guid Id { get; set; }

    // The edition's metadata, checked when the upload starts so a bad date fails before any part
    // is sent; applied when it finishes. Same meaning as publish_newsletter's arguments.
    public DateOnly IssueDate { get; set; }
    public string? Title { get; set; }
    public DateOnly? CoveredFrom { get; set; }
    public DateTime? CoveredUntil { get; set; }
    public int? ItemCount { get; set; }

    public List<NewsletterUploadPart> Parts { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One part of an upload, numbered from 1. Sending a part number again replaces it, so a retried
/// call (a timeout whose write had in fact landed) cannot duplicate content.
/// </summary>
public class NewsletterUploadPart
{
    public Guid Id { get; set; }
    public Guid UploadId { get; set; }
    public NewsletterUpload Upload { get; set; } = null!;
    public int PartNumber { get; set; }
    public string Html { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
