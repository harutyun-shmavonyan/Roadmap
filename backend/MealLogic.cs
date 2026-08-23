using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// Shared meal helpers used by both the REST endpoints the Nutrition tab talks to and the MCP
/// tools an assistant talks to — chiefly the photo, so both surfaces store and describe it the
/// same way.
/// </summary>
public static class MealLogic
{
    /// <summary>Photos are pictures of food, not archives: generous for a phone shot, far under Kestrel's cap.</summary>
    public const long MaxImageBytes = 20L * 1024 * 1024;

    public const string MaxImageHelp = "20 MB";

    /// <summary>
    /// Protein per calorie — the density the meal book is ordered by. Null when either number is
    /// missing (or the calories are zero), which sends the meal to the bottom of its slot rather
    /// than pretending it scored zero.
    /// </summary>
    public static double? ProteinDensity(Meal m) =>
        m.ProteinG is int protein && m.Calories is int kcal && kcal > 0 ? (double)protein / kcal : null;

    /// <summary>
    /// The book's order: within a slot, the most protein per calorie first, meals with nothing to
    /// compare on last, and the stored SortOrder as a stable tiebreak. Sorted in memory because the
    /// whole book is a handful of rows and the ratio is not a column.
    /// </summary>
    public static List<Meal> InBookOrder(IEnumerable<Meal> meals) =>
    [
        .. meals
            .OrderBy(m => m.Slot)
            .ThenByDescending(m => ProteinDensity(m) is not null)
            .ThenByDescending(m => ProteinDensity(m) ?? 0)
            .ThenBy(m => m.SortOrder)
            .ThenBy(m => m.CreatedAt)
    ];

    /// <summary>What a meal's photo is, without the bytes — cheap enough to ask for on every read.</summary>
    public record ImageMeta(string ContentType, DateTime UpdatedAt);

    public static MealDto ToDto(Meal m, ImageMeta? image = null) => new(
        m.Id, m.Slot.ToString(), m.Name, m.Summary,
        m.Ingredients, m.Steps, m.Calories, m.ProteinG, m.CarbsG, m.FatG, m.PrepMinutes,
        m.Tags, m.IsFavorite, m.SortOrder,
        image is not null, image?.ContentType, image?.UpdatedAt,
        m.CreatedAt, m.UpdatedAt);

    /// <summary>Metadata for one meal's photo, or null when it has none.</summary>
    public static async Task<ImageMeta?> ImageMetaAsync(RoadmapDbContext db, Guid mealId)
    {
        var row = await db.MealImages.AsNoTracking().Where(i => i.MealId == mealId)
            .Select(i => new { i.ContentType, i.UpdatedAt }).FirstOrDefaultAsync();
        return row is null ? null : new ImageMeta(row.ContentType, row.UpdatedAt);
    }

    /// <summary>Photo metadata for the whole book keyed by meal — one query, no bytes.</summary>
    public static async Task<Dictionary<Guid, ImageMeta>> ImageMetaMapAsync(RoadmapDbContext db)
    {
        var rows = await db.MealImages.AsNoTracking()
            .Select(i => new { i.MealId, i.ContentType, i.UpdatedAt }).ToListAsync();
        return rows.ToDictionary(r => r.MealId, r => new ImageMeta(r.ContentType, r.UpdatedAt));
    }

    /// <summary>Look up a meal's photo metadata in a map built by <see cref="ImageMetaMapAsync"/>.</summary>
    public static ImageMeta? Lookup(this Dictionary<Guid, ImageMeta> map, Guid mealId) =>
        map.TryGetValue(mealId, out var meta) ? meta : null;

    /// <summary>
    /// Settle on a MIME type for an upload: trust a declared image/* type, else read it off the
    /// bytes, else fall back to the filename's extension. Returns null when none of the three says
    /// "image", which is the signal to reject the upload — the tab renders these in an
    /// &lt;img&gt;, so a PDF here helps nobody.
    /// </summary>
    public static string? ResolveImageContentType(string? declared, string? fileName, byte[]? bytes = null)
    {
        var ct = declared?.Trim();
        if (IsImageType(ct)) return ct!.ToLowerInvariant();
        // The bytes are more trustworthy than a name: an assistant often has neither a type nor a
        // filename to offer, and a phone upload often declares application/octet-stream.
        if (bytes is not null && SniffImageType(bytes) is string sniffed) return sniffed;
        var guessed = string.IsNullOrWhiteSpace(fileName) ? null : ArticleLogic.GuessContentType(fileName!);
        return IsImageType(guessed) ? guessed : null;
    }

    /// <summary>The image type the bytes themselves declare, by magic number, or null.</summary>
    public static string? SniffImageType(byte[] b)
    {
        static bool Ascii(byte[] b, int at, string tag) =>
            b.Length >= at + tag.Length && !tag.Where((c, i) => b[at + i] != (byte)c).Any();

        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (Ascii(b, 0, "GIF8")) return "image/gif";
        if (Ascii(b, 0, "RIFF") && Ascii(b, 8, "WEBP")) return "image/webp";
        if (Ascii(b, 4, "ftyp") && (Ascii(b, 8, "avif") || Ascii(b, 8, "avis"))) return "image/avif";
        if (Ascii(b, 0, "BM")) return "image/bmp";
        return null;
    }

    private static bool IsImageType(string? ct) =>
        !string.IsNullOrWhiteSpace(ct) && ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Store (or replace) a meal's photo. The caller has already validated the bytes and type;
    /// this only writes the row and touches the meal so the tab sees it as changed. Does not save.
    /// </summary>
    public static async Task<MealImage> StoreImageAsync(
        RoadmapDbContext db, Meal meal, byte[] bytes, string contentType, string? fileName)
    {
        var now = DateTime.UtcNow;
        var image = await db.MealImages.FirstOrDefaultAsync(i => i.MealId == meal.Id);
        if (image is null)
        {
            image = new MealImage { MealId = meal.Id };
            db.MealImages.Add(image);
        }
        image.Data = bytes;
        image.ContentType = contentType;
        var clean = string.IsNullOrWhiteSpace(fileName) ? "" : ArticleLogic.SanitizeImageName(fileName);
        image.FileName = clean.Length == 0 ? null : clean;
        image.UpdatedAt = now;
        meal.UpdatedAt = now;
        return image;
    }
}
