using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

/// <summary>
/// The Experiences tab over MCP: the list of things planned and the record of things done, with
/// pictures. Same rows the tab writes through /api/experiences.
///
/// One deliberate difference from the REST surface, the same one the meal tools make: the tab's
/// PUT replaces a whole experience, but <see cref="UpdateExperience"/> patches — a null argument
/// keeps what is stored and an empty string is the explicit way to clear a field. An assistant
/// rarely restates everything it is not changing, and should not lose it for that.
/// </summary>
[McpServerToolType]
public sealed class ExperienceMcpTools(RoadmapDbContext db, IHttpClientFactory httpFactory)
{
    private static readonly JsonSerializerOptions Out = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
    private static string J(object? v) => JsonSerializer.Serialize(v, Out);

    private const string StatusHelp = "planned | done";

    private async Task<string> Echo(Guid id) =>
        J(ExperienceLogic.ToDto((await ExperienceLogic.LoadAsync(db, id: id))[0]));

    // ===== Read =====

    [McpServerTool(Name = "list_experiences"), Description(
        "List experiences from the Experiences tab: planned ones first, soonest first (undated plans after " +
        "the dated ones), then done ones, most recent first. Each carries its category, status, location, " +
        "dates, Markdown description and the metadata of its pictures (not the bytes). Filter by status " +
        "(" + StatusHelp + ") and/or category (case-insensitive).")]
    public async Task<string> ListExperiences(
        [Description(StatusHelp + " — omit for both")] string? status = null,
        [Description("Only this category, e.g. 'Travel' (case-insensitive)")] string? category = null)
    {
        ExperienceStatus? st = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!ExperienceLogic.TryParseStatus(status, out var s)) return J(new { error = "status must be " + StatusHelp });
            st = s;
        }
        var rows = await ExperienceLogic.LoadAsync(db, st, category);
        return J(rows.Select(ExperienceLogic.ToDto));
    }

    [McpServerTool(Name = "get_experience"), Description("One experience in full, with its pictures' metadata.")]
    public async Task<string> GetExperience([Description("Experience UUID")] Guid experience_id)
    {
        var x = (await ExperienceLogic.LoadAsync(db, id: experience_id)).FirstOrDefault();
        return x is null ? J(new { error = "experience not found" }) : J(ExperienceLogic.ToDto(x));
    }

    [McpServerTool(Name = "list_experience_categories"), Description(
        "The categories already in use, most used first, with a count each. Check this before creating: " +
        "categories are free text, and reusing an existing one ('Travel') is better than inventing a near " +
        "twin ('Trips'). Writes already fold case, so 'travel' lands on an existing 'Travel'.")]
    public async Task<string> ListExperienceCategories() =>
        J((await ExperienceLogic.CategoriesAsync(db)).Select(c => new { category = c.Category, count = c.Count }));

    // ===== Write =====

    [McpServerTool(Name = "create_experience"), Description(
        "Add an experience — something planned, or something already had. Only the title is required: " +
        "a plan is worth saving the moment it exists, long before it has a place, date or picture. Dates " +
        "are yyyy-MM-dd; an end date needs a start date and cannot precede it. description_md is Markdown. " +
        "Pictures are added afterwards with add_experience_image or add_experience_image_from_url.")]
    public async Task<string> CreateExperience(
        [Description("What it is, e.g. 'See the northern lights in Tromsø'")] string title,
        [Description("Category, e.g. 'Travel', 'Food', 'Music' — see list_experience_categories")] string? category = null,
        [Description(StatusHelp + " (default planned)")] string? status = null,
        [Description("Where, as you would write it, e.g. 'Tromsø, Norway'")] string? location = null,
        [Description("yyyy-MM-dd")] string? start_date = null,
        [Description("yyyy-MM-dd, for something that spans days")] string? end_date = null,
        [Description("The description, in Markdown")] string? description_md = null)
    {
        var errors = new List<string>();
        if (!ExperienceLogic.TryParseStatus(status, out var st)) errors.Add("status must be " + StatusHelp);
        if (!ExperienceLogic.TryParseDate(start_date, out var start)) errors.Add("start_date must be yyyy-MM-dd");
        if (!ExperienceLogic.TryParseDate(end_date, out var end)) errors.Add("end_date must be yyyy-MM-dd");
        errors.AddRange(ExperienceLogic.Validate(title, start, end, description_md));
        if (errors.Count > 0) return J(new { error = "validation", details = errors });

        var x = new Experience
        {
            Id = Guid.NewGuid(), Title = title.Trim(),
            Category = await ExperienceLogic.ResolveCategoryAsync(db, category),
            Status = st,
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            StartDate = start, EndDate = end,
            DescriptionMd = string.IsNullOrWhiteSpace(description_md) ? null : description_md,
        };
        db.Experiences.Add(x);
        await db.SaveChangesAsync();
        return await Echo(x.Id);
    }

    [McpServerTool(Name = "update_experience"), Description(
        "Change an experience. PATCH semantics: an argument you leave out keeps what is stored; an empty " +
        "string clears that field (category, location, a date, the description). The title cannot be " +
        "cleared. Dates are yyyy-MM-dd and are checked together, so moving only the start past the stored " +
        "end is refused.")]
    public async Task<string> UpdateExperience(
        [Description("Experience UUID")] Guid experience_id,
        [Description("New title")] string? title = null,
        [Description("New category, or '' to clear")] string? category = null,
        [Description(StatusHelp)] string? status = null,
        [Description("New location, or '' to clear")] string? location = null,
        [Description("yyyy-MM-dd, or '' to clear")] string? start_date = null,
        [Description("yyyy-MM-dd, or '' to clear")] string? end_date = null,
        [Description("New Markdown description, or '' to clear")] string? description_md = null)
    {
        var x = await db.Experiences.FirstOrDefaultAsync(e => e.Id == experience_id);
        if (x is null) return J(new { error = "experience not found" });

        var errors = new List<string>();
        var st = x.Status;
        if (status is not null && !ExperienceLogic.TryParseStatus(status, out st)) errors.Add("status must be " + StatusHelp);
        var start = x.StartDate;
        if (start_date is not null && !ExperienceLogic.TryParseDate(start_date, out start)) errors.Add("start_date must be yyyy-MM-dd");
        var end = x.EndDate;
        if (end_date is not null && !ExperienceLogic.TryParseDate(end_date, out end)) errors.Add("end_date must be yyyy-MM-dd");
        var newTitle = title ?? x.Title;
        errors.AddRange(ExperienceLogic.Validate(newTitle, start, end, description_md ?? x.DescriptionMd));
        if (errors.Count > 0) return J(new { error = "validation", details = errors });

        x.Title = newTitle.Trim();
        if (category is not null) x.Category = await ExperienceLogic.ResolveCategoryAsync(db, category);
        x.Status = st;
        if (location is not null) x.Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        x.StartDate = start;
        x.EndDate = end;
        if (description_md is not null) x.DescriptionMd = string.IsNullOrWhiteSpace(description_md) ? null : description_md;
        x.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await Echo(x.Id);
    }

    [McpServerTool(Name = "set_experience_status"), Description(
        "Mark an experience done — or back to planned. The quick way to record that it happened; use " +
        "update_experience at the same time if its dates or description should now say how it went.")]
    public async Task<string> SetExperienceStatus(
        [Description("Experience UUID")] Guid experience_id,
        [Description(StatusHelp)] string status)
    {
        var x = await db.Experiences.FirstOrDefaultAsync(e => e.Id == experience_id);
        if (x is null) return J(new { error = "experience not found" });
        if (string.IsNullOrWhiteSpace(status) || !ExperienceLogic.TryParseStatus(status, out var st))
            return J(new { error = "status must be " + StatusHelp });
        x.Status = st;
        x.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await Echo(x.Id);
    }

    [McpServerTool(Name = "delete_experience"), Description(
        "Permanently delete an experience and all its pictures. There is no undo.")]
    public async Task<string> DeleteExperience([Description("Experience UUID")] Guid experience_id)
    {
        var x = await db.Experiences.FirstOrDefaultAsync(e => e.Id == experience_id);
        if (x is null) return J(new { error = "experience not found" });
        var pictures = await db.ExperienceImages.CountAsync(i => i.ExperienceId == experience_id);
        db.Experiences.Remove(x);
        await db.SaveChangesAsync();
        return J(new { deleted = true, experienceId = experience_id, title = x.Title, picturesDeleted = pictures });
    }

    // ===== Pictures =====

    [McpServerTool(Name = "add_experience_image"), Description(
        "Add a picture to an experience — of the place beforehand, or of it once you were there. Pass the " +
        "bytes as base64 in data_base64 (a full 'data:image/...;base64,...' URI is also accepted). Pictures " +
        "accumulate; the first one is the cover. The type is read off the bytes, so content_type is rarely " +
        "needed. Images only, up to " + ImageLogic.MaxImageHelp + ". For a picture already on the web prefer " +
        "add_experience_image_from_url — the bytes never pass through the tool call.")]
    public async Task<string> AddExperienceImage(
        [Description("Experience UUID")] Guid experience_id,
        [Description("Image bytes as base64, or a full data: URI")] string data_base64,
        [Description("A line on what the picture shows (optional)")] string? caption = null,
        [Description("MIME type, e.g. 'image/jpeg' (optional)")] string? content_type = null,
        [Description("Original filename (optional — only names a re-download)")] string? file_name = null)
    {
        var x = await db.Experiences.FirstOrDefaultAsync(e => e.Id == experience_id);
        if (x is null) return J(new { error = "experience not found" });

        var (bytes, declared, error) = ImageLogic.DecodeBase64(data_base64);
        if (error is not null) return J(new { error });
        var ct = ImageLogic.ResolveContentType(content_type ?? declared, file_name, bytes);
        if (ct is null) return J(new { error = "that is not an image — pass an image/* content_type if the bytes are unusual" });

        var image = await ExperienceLogic.AddImageAsync(db, x, bytes!, ct, file_name, caption);
        await db.SaveChangesAsync();
        return J(new { added = true, image = ExperienceLogic.ToDto(image), bytes = bytes!.Length });
    }

    [McpServerTool(Name = "add_experience_image_from_url"), Description(
        "Add a picture to an experience by having the server download it from an http(s) URL — the bytes " +
        "never pass through the tool call, so there is no token cost and no base64 limit. The URL must be " +
        "publicly reachable (no login); it is fetched immediately, so a temporary signed URL is fine. " +
        "Private and loopback addresses are refused.")]
    public async Task<string> AddExperienceImageFromUrl(
        [Description("Experience UUID")] Guid experience_id,
        [Description("Public http(s) URL of the image")] string url,
        [Description("A line on what the picture shows (optional)")] string? caption = null,
        [Description("MIME type (optional — taken from the response)")] string? content_type = null)
    {
        var x = await db.Experiences.FirstOrDefaultAsync(e => e.Id == experience_id);
        if (x is null) return J(new { error = "experience not found" });

        var (bytes, headerType, fileName, error) = await ImageLogic.DownloadAsync(httpFactory, url);
        if (error is not null) return J(new { error });
        var ct = ImageLogic.ResolveContentType(content_type ?? headerType, fileName, bytes);
        if (ct is null) return J(new { error = $"that URL did not return an image (content type '{headerType ?? "unknown"}')" });

        var image = await ExperienceLogic.AddImageAsync(db, x, bytes!, ct, fileName, caption);
        await db.SaveChangesAsync();
        return J(new { added = true, image = ExperienceLogic.ToDto(image), bytes = bytes!.Length, sourceUrl = url });
    }

    [McpServerTool(Name = "update_experience_image"), Description(
        "Change a picture's caption or its place in the gallery (sort_order; the lowest is the cover). " +
        "An empty caption clears it.")]
    public async Task<string> UpdateExperienceImage(
        [Description("Image UUID (from the experience's images)")] Guid image_id,
        [Description("New caption, or '' to clear")] string? caption = null,
        [Description("New position; the lowest is the cover")] int? sort_order = null)
    {
        var img = await db.ExperienceImages.FirstOrDefaultAsync(i => i.Id == image_id);
        if (img is null) return J(new { error = "image not found" });
        if (caption is not null)
            img.Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim()[..Math.Min(caption.Trim().Length, 512)];
        if (sort_order is int so) img.SortOrder = so;
        var x = await db.Experiences.FirstAsync(e => e.Id == img.ExperienceId);
        x.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(new { updated = true, image = ExperienceLogic.ToDto(img) });
    }

    [McpServerTool(Name = "delete_experience_image"), Description(
        "Remove one picture from an experience. The experience and its other pictures are untouched.")]
    public async Task<string> DeleteExperienceImage([Description("Image UUID")] Guid image_id)
    {
        var img = await db.ExperienceImages.FirstOrDefaultAsync(i => i.Id == image_id);
        if (img is null) return J(new { error = "image not found" });
        db.ExperienceImages.Remove(img);
        var x = await db.Experiences.FirstAsync(e => e.Id == img.ExperienceId);
        x.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(new { deleted = true, imageId = image_id, experienceId = x.Id });
    }
}
