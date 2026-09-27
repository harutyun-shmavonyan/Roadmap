using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Endpoints;

/// <summary>
/// The Experiences tab. Full CRUD from the UI — unlike courses, an experience is something a
/// person writes by hand as readily as an agent does — so this is shaped like the meal book:
/// PUT replaces the whole experience, and pictures are uploaded as multipart and read back as
/// bytes behind the same bearer token as everything else.
/// </summary>
public static class ExperienceEndpoints
{
    public static void MapExperienceEndpoints(this WebApplication app)
    {
        var xp = app.MapGroup("/api/experiences").WithTags("Experiences").RequireAuthorization();

        xp.MapGet("/", async (string? status, string? category, string? tag, RoadmapDbContext db) =>
        {
            ExperienceStatus? st = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!ExperienceLogic.TryParseStatus(status, out var s))
                    return Results.BadRequest("status must be planned or done.");
                st = s;
            }
            var rows = await ExperienceLogic.LoadAsync(db, st, category, tag: tag);
            return Results.Ok(rows.Select(ExperienceLogic.ToDto));
        });

        xp.MapGet("/categories", async (RoadmapDbContext db) =>
            Results.Ok((await ExperienceLogic.CategoriesAsync(db))
                .Select(c => new { category = c.Category, count = c.Count })));

        xp.MapGet("/tags", async (RoadmapDbContext db) =>
            Results.Ok((await ExperienceLogic.TagsAsync(db)).Select(t => new { tag = t.Tag, count = t.Count })));

        xp.MapGet("/{id:guid}", async (Guid id, RoadmapDbContext db) =>
        {
            var x = (await ExperienceLogic.LoadAsync(db, id: id)).FirstOrDefault();
            return x is null ? Results.NotFound() : Results.Ok(ExperienceLogic.ToDto(x));
        });

        xp.MapPost("/", async (SaveExperienceRequest req, RoadmapDbContext db) =>
        {
            var x = new Experience { Id = Guid.NewGuid() };
            var error = await Apply(db, x, req);
            if (error is not null) return error;
            db.Experiences.Add(x);
            await db.SaveChangesAsync();
            return Results.Ok(ExperienceLogic.ToDto((await ExperienceLogic.LoadAsync(db, id: x.Id))[0]));
        });

        xp.MapPut("/{id:guid}", async (Guid id, SaveExperienceRequest req, RoadmapDbContext db) =>
        {
            var x = await db.Experiences.FirstOrDefaultAsync(e => e.Id == id);
            if (x is null) return Results.NotFound();
            var error = await Apply(db, x, req);
            if (error is not null) return error;
            x.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ExperienceLogic.ToDto((await ExperienceLogic.LoadAsync(db, id: id))[0]));
        });

        // The one-tap "I did it" (or "not yet"), so marking something done is not a form.
        xp.MapPatch("/{id:guid}/status", async (Guid id, SetExperienceStatusRequest req, RoadmapDbContext db) =>
        {
            var x = await db.Experiences.FirstOrDefaultAsync(e => e.Id == id);
            if (x is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(req.Status) || !ExperienceLogic.TryParseStatus(req.Status, out var st))
                return Results.BadRequest("status must be planned or done.");
            x.Status = st;
            x.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ExperienceLogic.ToDto((await ExperienceLogic.LoadAsync(db, id: id))[0]));
        });

        xp.MapDelete("/{id:guid}", async (Guid id, RoadmapDbContext db) =>
        {
            var x = await db.Experiences.FirstOrDefaultAsync(e => e.Id == id);
            if (x is null) return Results.NotFound();
            db.Experiences.Remove(x); // pictures cascade
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ── pictures ──

        // One or more files in one request: a phone's photo picker hands over a handful at once.
        xp.MapPost("/{id:guid}/images", async (Guid id, HttpRequest request, RoadmapDbContext db) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest("Expected multipart/form-data.");
            var x = await db.Experiences.FirstOrDefaultAsync(e => e.Id == id);
            if (x is null) return Results.NotFound();

            var form = await request.ReadFormAsync();
            var files = form.Files.Where(f => f.Length > 0).ToList();
            if (files.Count == 0) return Results.BadRequest("No image uploaded.");
            var caption = form["caption"].FirstOrDefault();

            foreach (var file in files)
            {
                if (file.Length > ImageLogic.MaxImageBytes)
                    return Results.BadRequest($"{file.FileName} exceeds {ImageLogic.MaxImageHelp}.");
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();
                var ct = ImageLogic.ResolveContentType(file.ContentType, file.FileName, bytes);
                if (ct is null) return Results.BadRequest($"{file.FileName} is not an image (png, jpeg, gif, webp, avif).");
                // A caption only makes sense for a single picture; a batch arrives uncaptioned.
                await ExperienceLogic.AddImageAsync(db, x, bytes, ct, file.FileName, files.Count == 1 ? caption : null);
                await db.SaveChangesAsync(); // per file, so each takes the next position
            }
            return Results.Ok(ExperienceLogic.ToDto((await ExperienceLogic.LoadAsync(db, id: id))[0]));
        }).DisableAntiforgery();

        // Raw bytes. Fetched with the bearer token and turned into an object URL by the client,
        // rather than pointed at by an <img>, which would not carry the token.
        xp.MapGet("/images/{imageId:guid}", async (Guid imageId, RoadmapDbContext db) =>
        {
            var img = await db.ExperienceImages.AsNoTracking().FirstOrDefaultAsync(i => i.Id == imageId);
            return img is null ? Results.NotFound() : Results.File(img.Data, img.ContentType);
        });

        xp.MapPatch("/images/{imageId:guid}", async (Guid imageId, UpdateExperienceImageRequest req, RoadmapDbContext db) =>
        {
            var img = await db.ExperienceImages.FirstOrDefaultAsync(i => i.Id == imageId);
            if (img is null) return Results.NotFound();
            if (req.Caption is not null)
                img.Caption = string.IsNullOrWhiteSpace(req.Caption) ? null : req.Caption.Trim()[..Math.Min(req.Caption.Trim().Length, 512)];
            if (req.SortOrder is int so) img.SortOrder = so;
            var x = await db.Experiences.FirstAsync(e => e.Id == img.ExperienceId);
            x.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ExperienceLogic.ToDto(img));
        });

        xp.MapDelete("/images/{imageId:guid}", async (Guid imageId, RoadmapDbContext db) =>
        {
            var img = await db.ExperienceImages.FirstOrDefaultAsync(i => i.Id == imageId);
            if (img is null) return Results.NotFound();
            db.ExperienceImages.Remove(img);
            var x = await db.Experiences.FirstAsync(e => e.Id == img.ExperienceId);
            x.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// <summary>Write a whole form onto an experience, or say what is wrong with it.</summary>
    private static async Task<IResult?> Apply(RoadmapDbContext db, Experience x, SaveExperienceRequest req)
    {
        var errors = new List<string>();
        if (!ExperienceLogic.TryParseStatus(req.Status, out var status)) errors.Add("status must be planned or done");
        if (!ExperienceLogic.TryParseDate(req.StartDate, out var start)) errors.Add("start_date must be yyyy-MM-dd");
        if (!ExperienceLogic.TryParseDate(req.EndDate, out var end)) errors.Add("end_date must be yyyy-MM-dd");
        errors.AddRange(ExperienceLogic.Validate(req.Title, start, end, req.DescriptionMd));
        var (tags, tagError) = await ExperienceLogic.ResolveTagsAsync(db, req.Tags, x.Id);
        if (tagError is not null) errors.Add(tagError);
        if (errors.Count > 0) return Results.BadRequest(new { error = "validation", details = errors });

        x.Title = req.Title!.Trim();
        x.Category = await ExperienceLogic.ResolveCategoryAsync(db, req.Category);
        x.Status = status;
        x.Location = string.IsNullOrWhiteSpace(req.Location) ? null : req.Location.Trim();
        x.StartDate = start;
        x.EndDate = end;
        x.DescriptionMd = string.IsNullOrWhiteSpace(req.DescriptionMd) ? null : req.DescriptionMd;
        x.Tags = tags; // PUT replaces: a form that sends no tags clears them, like every other field
        return null;
    }
}
