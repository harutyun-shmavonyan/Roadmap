using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// Everything about an experience that the REST endpoints and the MCP tools must agree on: how
/// a date is read, what a category resolves to, what order the list comes back in, and how a
/// picture is stored. Static and shared for the same reason as <c>NewsletterLogic</c> — two
/// surfaces write the same rows, and the only way they cannot drift is to not each have a copy.
/// </summary>
public static partial class ExperienceLogic
{
    public const int MaxDescriptionBytes = 200 * 1024;

    // ── reading input ──

    public static bool TryParseStatus(string? raw, out ExperienceStatus status)
    {
        status = ExperienceStatus.Planned;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        return Enum.TryParse(raw.Trim(), ignoreCase: true, out status)
            && Enum.IsDefined(status);
    }

    /// <summary>"planned" | "done" — the wire form, lower-case like the rest of the newer tabs.</summary>
    public static string Wire(ExperienceStatus s) => s.ToString().ToLowerInvariant();

    /// <summary>A yyyy-MM-dd date, or null for blank. Anything else is an error, not a guess.</summary>
    public static bool TryParseDate(string? raw, out DateOnly? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        if (DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d)) { date = d; return true; }
        return false;
    }

    /// <summary>
    /// Everything wrong with an experience as it would be saved, or nothing. Collected rather than
    /// thrown one at a time, so a caller fixing a form fixes it in one pass.
    /// </summary>
    public static List<string> Validate(string? title, DateOnly? start, DateOnly? end, string? descriptionMd)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(title)) errors.Add("title is required");
        else if (title.Trim().Length > 200) errors.Add("title is longer than 200 characters");
        if (start is DateOnly s && end is DateOnly e && e < s)
            errors.Add("end_date is before start_date");
        if (end is not null && start is null)
            errors.Add("end_date needs a start_date — an experience cannot end without beginning");
        if (descriptionMd is not null && Encoding.UTF8.GetByteCount(descriptionMd) > MaxDescriptionBytes)
            errors.Add("description is larger than 200 KB");
        return errors;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>
    /// The category as it will be stored: trimmed, inner whitespace collapsed, and — if one already
    /// in use matches case-insensitively — spelled the way that one is. A free-text vocabulary
    /// drifts ("Travel", "travel", "travel ") the first time two sessions write it; this is what
    /// stops that without making the vocabulary closed.
    /// </summary>
    public static async Task<string?> ResolveCategoryAsync(RoadmapDbContext db, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var clean = Spaces().Replace(raw.Trim(), " ");
        if (clean.Length > 64) clean = clean[..64].TrimEnd();
        var lower = clean.ToLowerInvariant();
        var existing = await db.Experiences.AsNoTracking()
            .Where(x => x.Category != null && x.Category.ToLower() == lower)
            .Select(x => x.Category).FirstOrDefaultAsync();
        return existing ?? clean;
    }

    /// <summary>Categories in use, most used first — what a form offers and an agent should reuse.</summary>
    public static async Task<List<(string Category, int Count)>> CategoriesAsync(RoadmapDbContext db)
    {
        var rows = await db.Experiences.AsNoTracking().Where(x => x.Category != null)
            .GroupBy(x => x.Category!).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return [.. rows.OrderByDescending(r => r.Count).ThenBy(r => r.Key).Select(r => (r.Key, r.Count))];
    }

    // ── reading out ──

    public static ExperienceImageDto ToDto(ExperienceImage i) =>
        new(i.Id, i.ContentType, i.Caption, i.FileName, i.SortOrder, i.CreatedAt);

    /// <summary>The images must be loaded without their bytes — see <see cref="LoadAsync"/>.</summary>
    public static ExperienceDto ToDto(Experience x) => new(
        x.Id, x.Title, x.Category, Wire(x.Status), x.Location,
        x.StartDate?.ToString("yyyy-MM-dd"), x.EndDate?.ToString("yyyy-MM-dd"), x.DescriptionMd,
        [.. x.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.CreatedAt).Select(ToDto)],
        x.CreatedAt, x.UpdatedAt);

    /// <summary>
    /// Experiences with their image metadata but never the image bytes, which can be megabytes each
    /// and are only ever wanted one at a time.
    /// </summary>
    public static async Task<List<Experience>> LoadAsync(RoadmapDbContext db,
        ExperienceStatus? status = null, string? category = null, Guid? id = null)
    {
        var q = db.Experiences.AsNoTracking().AsQueryable();
        if (id is Guid one) q = q.Where(x => x.Id == one);
        if (status is ExperienceStatus st) q = q.Where(x => x.Status == st);
        if (!string.IsNullOrWhiteSpace(category))
        {
            var lower = category.Trim().ToLowerInvariant();
            q = q.Where(x => x.Category != null && x.Category.ToLower() == lower);
        }
        var rows = await q.ToListAsync();
        var ids = rows.Select(r => r.Id).ToList();
        var images = await db.ExperienceImages.AsNoTracking().Where(i => ids.Contains(i.ExperienceId))
            .Select(i => new ExperienceImage
            {
                Id = i.Id, ExperienceId = i.ExperienceId, ContentType = i.ContentType, FileName = i.FileName,
                Caption = i.Caption, SortOrder = i.SortOrder, CreatedAt = i.CreatedAt,
            }).ToListAsync();
        var byExp = images.ToLookup(i => i.ExperienceId);
        foreach (var r in rows) r.Images = [.. byExp[r.Id]];
        return InListOrder(rows);
    }

    /// <summary>
    /// Planned first, soonest first, with undated plans after the dated ones (newest idea first);
    /// then done, most recent first. That is the order a person reads a bucket list in: what is
    /// coming, then what was had.
    /// </summary>
    public static List<Experience> InListOrder(IEnumerable<Experience> xs)
    {
        var list = xs.ToList();
        var planned = list.Where(x => x.Status == ExperienceStatus.Planned)
            .OrderBy(x => x.StartDate is null).ThenBy(x => x.StartDate).ThenByDescending(x => x.CreatedAt);
        var done = list.Where(x => x.Status == ExperienceStatus.Done)
            .OrderBy(x => (x.EndDate ?? x.StartDate) is null)
            .ThenByDescending(x => x.EndDate ?? x.StartDate).ThenByDescending(x => x.UpdatedAt);
        return [.. planned, .. done];
    }

    // ── pictures ──

    /// <summary>Add one picture at the end of the gallery. Validated by the caller; does not save.</summary>
    public static async Task<ExperienceImage> AddImageAsync(RoadmapDbContext db, Experience x,
        byte[] bytes, string contentType, string? fileName, string? caption)
    {
        var next = (await db.ExperienceImages.Where(i => i.ExperienceId == x.Id)
            .Select(i => (int?)i.SortOrder).MaxAsync() ?? -1) + 1;
        var image = new ExperienceImage
        {
            Id = Guid.NewGuid(), ExperienceId = x.Id, Data = bytes, ContentType = contentType,
            FileName = ImageLogic.CleanFileName(fileName),
            Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim()[..Math.Min(caption.Trim().Length, 512)],
            SortOrder = next,
        };
        db.ExperienceImages.Add(image);
        x.UpdatedAt = DateTime.UtcNow;
        return image;
    }
}
