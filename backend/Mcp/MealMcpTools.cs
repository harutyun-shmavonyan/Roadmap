using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

/// <summary>
/// CRUD over the meal book behind the Nutrition tab — the same store the UI writes to
/// via /api/meals.
///
/// One deliberate difference from the REST surface: the PUT there replaces a whole meal,
/// so a missing list clears it. That is wrong for an assistant, which rarely restates
/// every field it is not changing, so <see cref="UpdateMeal"/> patches instead — a null
/// argument keeps the stored value and an empty array is the explicit way to clear a list.
/// </summary>
[McpServerToolType]
public sealed class MealMcpTools(RoadmapDbContext db, IHttpClientFactory httpFactory)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    private const string SlotHelp = "Breakfast | Lunch | Dinner | Snack";

    private static bool TryParseSlot(string? raw, MealSlot fallback, out MealSlot value)
    {
        if (string.IsNullOrWhiteSpace(raw)) { value = fallback; return true; }
        return Enum.TryParse(raw, ignoreCase: true, out value);
    }

    // Trim every line and drop the blanks — callers paste ragged lists.
    private static List<string> CleanList(IEnumerable<string>? raw) =>
        raw is null ? [] : [.. raw.Select(s => (s ?? "").Trim()).Where(s => s.Length > 0)];

    // Negative macros are meaningless; clamp rather than reject so a typo doesn't lose the meal.
    private static int? NonNegative(int? v) => v is null ? null : Math.Max(0, v.Value);

    private static MealDto ToDto(Meal m, MealLogic.ImageMeta? image = null) => MealLogic.ToDto(m, image);

    /// <summary>A meal plus its photo metadata — every single-meal reply carries has_image.</summary>
    private async Task<MealDto> ToDtoWithImage(Meal m) => ToDto(m, await MealLogic.ImageMetaAsync(db, m.Id));

    /// <summary>Next free position at the end of a slot.</summary>
    private async Task<int> NextSortOrder(MealSlot slot) =>
        (await db.Meals.Where(m => m.Slot == slot).Select(m => (int?)m.SortOrder).MaxAsync() ?? -1) + 1;

    // ===== Read =====

    [McpServerTool(Name = "list_meals"), Description(
        "List saved meals from the Nutrition tab's meal book, favourites first within each slot. " +
        "Pass slot to narrow to one part of the day; omit it for the whole book.")]
    public async Task<string> ListMeals(
        [Description(SlotHelp + " — omit for all slots")] string? slot = null,
        [Description("Only meals tagged with this label, e.g. \"high-protein\" (case-insensitive)")] string? tag = null,
        [Description("Only starred meals")] bool favorites_only = false)
    {
        MealSlot? filter = null;
        if (!string.IsNullOrWhiteSpace(slot))
        {
            if (!Enum.TryParse<MealSlot>(slot, ignoreCase: true, out var parsed))
                return J(new { error = $"invalid slot '{slot}'. Use {SlotHelp}." });
            filter = parsed;
        }

        var q = db.Meals.AsNoTracking();
        if (filter is not null) q = q.Where(m => m.Slot == filter);
        if (favorites_only) q = q.Where(m => m.IsFavorite);

        var list = await q
            .OrderBy(m => m.Slot)
            .ThenByDescending(m => m.IsFavorite)
            .ThenBy(m => m.SortOrder)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync();

        // Tag matching is done in memory: the tags are a serialised list, not a queryable column.
        if (!string.IsNullOrWhiteSpace(tag))
            list = [.. list.Where(m => m.Tags.Any(t => string.Equals(t, tag.Trim(), StringComparison.OrdinalIgnoreCase)))];

        var images = await MealLogic.ImageMetaMapAsync(db);
        return J(new { count = list.Count, meals = list.Select(m => ToDto(m, images.Lookup(m.Id))) });
    }

    [McpServerTool(Name = "get_meal"), Description(
        "Get one meal in full — ingredients, steps, macros, tags, and whether it has a photo " +
        "(has_image). The photo bytes are not returned; the tab renders them from /api/meals/{id}/image.")]
    public async Task<string> GetMeal([Description("Meal UUID")] Guid meal_id)
    {
        var meal = await db.Meals.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meal_id);
        return meal is null ? J(new { error = "meal not found" }) : J(await ToDtoWithImage(meal));
    }

    // ===== Write =====

    [McpServerTool(Name = "create_meal"), Description(
        "Save a meal worth keeping to the Nutrition tab. This is a cookbook of what to eat, " +
        "not a food log — save a meal when it earns a repeat, not to record that it was eaten. " +
        "Every macro is optional and can be filled in later. The meal lands at the end of its slot. " +
        "Photos are set separately: call set_meal_image_from_url (or set_meal_image) afterwards with " +
        "the id this returns.")]
    public async Task<string> CreateMeal(
        [Description("What it's called, e.g. \"Greek yogurt, berries & walnuts\"")] string name,
        [Description(SlotHelp + " (default Breakfast)")] string? slot = null,
        [Description("One line on why it earns its place")] string? summary = null,
        [Description("What goes in it, one entry per ingredient, with amounts")] string[]? ingredients = null,
        [Description("How it's made, one entry per step. Omit for assembly-only meals.")] string[]? steps = null,
        [Description("Calories per serving")] int? calories = null,
        [Description("Protein grams per serving")] int? protein_g = null,
        [Description("Carb grams per serving")] int? carbs_g = null,
        [Description("Fat grams per serving")] int? fat_g = null,
        [Description("Hands-on minutes")] int? prep_minutes = null,
        [Description("Labels, e.g. \"high-protein\", \"no-cook\", \"post-workout\"")] string[]? tags = null,
        [Description("Star it so it sorts to the front of its slot")] bool is_favorite = false)
    {
        var clean = (name ?? "").Trim();
        if (clean.Length == 0) return J(new { error = "name is required" });
        if (!TryParseSlot(slot, MealSlot.Breakfast, out var slotValue))
            return J(new { error = $"invalid slot '{slot}'. Use {SlotHelp}." });

        var meal = new Meal
        {
            Id = Guid.NewGuid(),
            Slot = slotValue,
            SortOrder = await NextSortOrder(slotValue),
            Name = clean,
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
            Ingredients = CleanList(ingredients),
            Steps = CleanList(steps),
            Calories = NonNegative(calories),
            ProteinG = NonNegative(protein_g),
            CarbsG = NonNegative(carbs_g),
            FatG = NonNegative(fat_g),
            PrepMinutes = NonNegative(prep_minutes),
            Tags = CleanList(tags),
            IsFavorite = is_favorite,
        };
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        return J(new { status = "created", meal = ToDto(meal) }); // brand new — no photo yet
    }

    [McpServerTool(Name = "update_meal"), Description(
        "Update a saved meal in place. Only the arguments you pass change — anything omitted keeps " +
        "its stored value, so a tweak to one macro cannot wipe the recipe. To clear a list, pass an " +
        "empty array. Moving a meal to another slot puts it at the end of that slot.")]
    public async Task<string> UpdateMeal(
        [Description("Meal UUID")] Guid meal_id,
        [Description("New name")] string? name = null,
        [Description(SlotHelp)] string? slot = null,
        [Description("New one-line rationale")] string? summary = null,
        [Description("Replaces the ingredient list; empty array clears it")] string[]? ingredients = null,
        [Description("Replaces the steps; empty array clears them")] string[]? steps = null,
        [Description("Calories per serving")] int? calories = null,
        [Description("Protein grams per serving")] int? protein_g = null,
        [Description("Carb grams per serving")] int? carbs_g = null,
        [Description("Fat grams per serving")] int? fat_g = null,
        [Description("Hands-on minutes")] int? prep_minutes = null,
        [Description("Replaces the tags; empty array clears them")] string[]? tags = null,
        [Description("Star or unstar it")] bool? is_favorite = null)
    {
        var meal = await db.Meals.FirstOrDefaultAsync(m => m.Id == meal_id);
        if (meal is null) return J(new { error = "meal not found" });

        if (name is not null)
        {
            var clean = name.Trim();
            if (clean.Length == 0) return J(new { error = "name cannot be blank" });
            meal.Name = clean;
        }

        if (!string.IsNullOrWhiteSpace(slot))
        {
            if (!Enum.TryParse<MealSlot>(slot, ignoreCase: true, out var slotValue))
                return J(new { error = $"invalid slot '{slot}'. Use {SlotHelp}." });
            if (slotValue != meal.Slot)
            {
                meal.SortOrder = await NextSortOrder(slotValue);
                meal.Slot = slotValue;
            }
        }

        if (summary is not null) meal.Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        if (ingredients is not null) meal.Ingredients = CleanList(ingredients);
        if (steps is not null) meal.Steps = CleanList(steps);
        if (calories is not null) meal.Calories = NonNegative(calories);
        if (protein_g is not null) meal.ProteinG = NonNegative(protein_g);
        if (carbs_g is not null) meal.CarbsG = NonNegative(carbs_g);
        if (fat_g is not null) meal.FatG = NonNegative(fat_g);
        if (prep_minutes is not null) meal.PrepMinutes = NonNegative(prep_minutes);
        if (tags is not null) meal.Tags = CleanList(tags);
        if (is_favorite is not null) meal.IsFavorite = is_favorite.Value;

        meal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(new { status = "updated", meal = await ToDtoWithImage(meal) });
    }

    [McpServerTool(Name = "delete_meal"), Description("Permanently delete a meal from the meal book.")]
    public async Task<string> DeleteMeal([Description("Meal UUID")] Guid meal_id)
    {
        var meal = await db.Meals.FirstOrDefaultAsync(m => m.Id == meal_id);
        if (meal is null) return J(new { error = "meal not found" });
        db.Meals.Remove(meal);
        await db.SaveChangesAsync();
        return J(new { status = "deleted", name = meal.Name });
    }

    // ===== The meal photo (optional, one per meal) =====

    [McpServerTool(Name = "set_meal_image"), Description(
        "Set (or replace) the photo shown for a meal on the Nutrition tab. Pass the raw bytes as " +
        "base64 in data_base64 (a full 'data:image/...;base64,...' URI is also accepted). One photo " +
        "per meal — setting a new one replaces the old. The image type is read off the bytes, so " +
        "content_type is only needed for something unusual. Only image types are accepted, up to " +
        MealLogic.MaxImageHelp + ". For an image already on the web, prefer set_meal_image_from_url: " +
        "the bytes never pass through the tool call.")]
    public async Task<string> SetMealImage(
        [Description("Meal UUID")] Guid meal_id,
        [Description("Image bytes as base64 (or a full data: URI)")] string data_base64,
        [Description("MIME type, e.g. 'image/jpeg' (optional — read from a data: URI or guessed from file_name)")] string? content_type = null,
        [Description("Original filename, e.g. 'oats.jpg' (optional — only used to name the file and guess its type)")] string? file_name = null)
    {
        var meal = await db.Meals.FirstOrDefaultAsync(m => m.Id == meal_id);
        if (meal is null) return J(new { error = "meal not found" });

        // Accept a bare base64 string or a full data: URI (strip the 'data:...;base64,' prefix).
        var b64 = data_base64?.Trim() ?? "";
        var mime = content_type;
        if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = b64.IndexOf(',');
            if (comma < 0) return J(new { error = "malformed data URI" });
            var header = b64[5..comma]; // e.g. "image/png;base64"
            if (string.IsNullOrWhiteSpace(mime) && header.Length > 0) mime = header.Split(';')[0];
            b64 = b64[(comma + 1)..];
        }

        byte[] bytes;
        try { bytes = Convert.FromBase64String(b64); }
        catch (FormatException) { return J(new { error = "data_base64 is not valid base64" }); }
        if (bytes.Length == 0) return J(new { error = "image data is empty" });
        // Even under this cap, base64 through a tool call is bounded by the model's context —
        // for anything more than a couple of MB, use set_meal_image_from_url or the UI upload.
        if (bytes.Length > MealLogic.MaxImageBytes) return J(new { error = $"image exceeds {MealLogic.MaxImageHelp}" });

        var ct = MealLogic.ResolveImageContentType(mime, file_name, bytes);
        if (ct is null) return J(new { error = "that is not an image — pass an image/* content_type if the bytes are unusual" });

        var image = await MealLogic.StoreImageAsync(db, meal, bytes, ct, file_name);
        await db.SaveChangesAsync();
        return J(new { status = "image set", meal_id, content_type = image.ContentType, bytes = bytes.Length });
    }

    [McpServerTool(Name = "set_meal_image_from_url"), Description(
        "Set (or replace) a meal's photo by having the server download it from an http(s) URL — the " +
        "image bytes never pass through the tool call, so there is no token cost or base64 size limit. " +
        "Ideal for an AI-generated image at its hosted URL, or any picture already on the web. The URL " +
        "must be publicly reachable by the server (no login/cookies); it is fetched immediately, so " +
        "temporary signed URLs are fine.")]
    public async Task<string> SetMealImageFromUrl(
        [Description("Meal UUID")] Guid meal_id,
        [Description("Public http(s) URL of the image to download")] string url,
        [Description("MIME type (optional — taken from the response, else guessed from the URL's filename)")] string? content_type = null)
    {
        var meal = await db.Meals.FirstOrDefaultAsync(m => m.Id == meal_id);
        if (meal is null) return J(new { error = "meal not found" });

        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return J(new { error = "url must be an absolute http(s) URL" });
        if (await UrlGuard.IsBlockedHostAsync(uri))
            return J(new { error = "url host is not allowed (private/loopback addresses are blocked)" });

        byte[] bytes;
        string? headerType;
        try
        {
            var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RoadmapBot/1.0");
            using var resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode) return J(new { error = $"fetch failed: HTTP {(int)resp.StatusCode}" });
            headerType = resp.Content.Headers.ContentType?.MediaType;

            await using var src = await resp.Content.ReadAsStreamAsync();
            using var ms = new MemoryStream();
            var buf = new byte[81920];
            int read;
            while ((read = await src.ReadAsync(buf)) > 0)
            {
                if (ms.Length + read > MealLogic.MaxImageBytes) return J(new { error = $"image exceeds {MealLogic.MaxImageHelp}" });
                ms.Write(buf, 0, read);
            }
            bytes = ms.ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return J(new { error = $"could not download the image: {ex.Message}" });
        }
        if (bytes.Length == 0) return J(new { error = "downloaded image is empty" });

        var fileName = Path.GetFileName(uri.LocalPath);
        var ct = MealLogic.ResolveImageContentType(content_type ?? headerType, fileName, bytes);
        if (ct is null) return J(new { error = $"that URL did not return an image (content type '{headerType ?? "unknown"}')" });

        var image = await MealLogic.StoreImageAsync(db, meal, bytes, ct, fileName);
        await db.SaveChangesAsync();
        return J(new { status = "image set", meal_id, content_type = image.ContentType, bytes = bytes.Length, source_url = uri.ToString() });
    }

    [McpServerTool(Name = "delete_meal_image"), Description(
        "Remove a meal's photo. The meal itself is untouched — it just goes back to showing no picture.")]
    public async Task<string> DeleteMealImage([Description("Meal UUID")] Guid meal_id)
    {
        var image = await db.MealImages.FirstOrDefaultAsync(i => i.MealId == meal_id);
        if (image is null) return J(new { error = "that meal has no image" });
        db.MealImages.Remove(image);
        var meal = await db.Meals.FirstOrDefaultAsync(m => m.Id == meal_id);
        if (meal is not null) meal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(new { status = "image deleted", meal_id });
    }
}
