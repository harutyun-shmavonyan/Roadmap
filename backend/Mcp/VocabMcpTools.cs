using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

/// <summary>
/// The English-vocabulary surface: save an item, pull what is due, record how well it
/// was recalled. Scheduling is deliberately NOT exposed — the caller supplies a grade
/// and the server decides when the item comes back (see <see cref="Sm2"/>), so an
/// assistant driving a review cannot invent its own intervals.
/// </summary>
[McpServerToolType]
public sealed class VocabMcpTools(RoadmapDbContext db)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    private static bool TryEnum<T>(string? raw, T fallback, out T value) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw)) { value = fallback; return true; }
        return Enum.TryParse(raw, ignoreCase: true, out value);
    }

    // ===== Write =====

    [McpServerTool(Name = "add_vocab_entry"), Description(
        "Save an English word, idiom, phrasal verb or collocation to the vocabulary store so it enters spaced repetition. " +
        "Call this ONLY when the learner explicitly asks to save/remember the item — not for every word explained. " +
        "The new entry is due for review immediately. Re-saving an existing term updates it in place instead of duplicating.")]
    public async Task<string> AddVocabEntry(
        [Description("The item itself, e.g. \"bite the bullet\"")] string term,
        [Description("English definition")] string definition,
        [Description("Word | Idiom | PhrasalVerb | Collocation (default Word)")] string? kind = null,
        [Description("How common it is: VeryCommon | Common | Uncommon | Rare (default Common)")] string? frequency = null,
        [Description("Register: Formal | Neutral | Informal | Slang | Vulgar (default Neutral)")] string? register = null,
        [Description("Armenian gloss")] string? gloss_hy = null,
        [Description("Russian gloss")] string? gloss_ru = null,
        [Description("Natural example sentences")] string[]? examples = null,
        [Description("Words it habitually pairs with")] string[]? collocations = null,
        [Description("Close synonyms")] string[]? synonyms = null,
        [Description("The mnemonic / etymology / story that makes it stick")] string? memory_hook = null,
        [Description("Where the learner met it, or why they asked")] string? source_context = null,
        [Description("Anything else worth keeping")] string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(term)) return J(new { error = "term is required" });
        if (string.IsNullOrWhiteSpace(definition)) return J(new { error = "definition is required" });

        if (!TryEnum<VocabKind>(kind, VocabKind.Word, out var k))
            return J(new { error = $"invalid kind '{kind}'. Use Word | Idiom | PhrasalVerb | Collocation." });
        if (!TryEnum<VocabFrequency>(frequency, VocabFrequency.Common, out var f))
            return J(new { error = $"invalid frequency '{frequency}'. Use VeryCommon | Common | Uncommon | Rare." });
        if (!TryEnum<VocabRegister>(register, VocabRegister.Neutral, out var r))
            return J(new { error = $"invalid register '{register}'. Use Formal | Neutral | Informal | Slang | Vulgar." });

        var normalized = VocabStore.Normalize(term);
        var existing = await db.VocabEntries.Include(v => v.Reviews)
            .FirstOrDefaultAsync(v => v.NormalizedTerm == normalized);

        if (existing is not null)
        {
            // Enrich in place — never duplicate, and never wipe an existing field with a null.
            existing.Definition = definition;
            existing.Kind = k;
            existing.Frequency = f;
            existing.Register = r;
            existing.GlossHy = gloss_hy ?? existing.GlossHy;
            existing.GlossRu = gloss_ru ?? existing.GlossRu;
            existing.Examples = examples?.ToList() ?? existing.Examples;
            existing.Collocations = collocations?.ToList() ?? existing.Collocations;
            existing.Synonyms = synonyms?.ToList() ?? existing.Synonyms;
            existing.MemoryHook = memory_hook ?? existing.MemoryHook;
            existing.SourceContext = source_context ?? existing.SourceContext;
            existing.Notes = notes ?? existing.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return J(new { status = "updated_existing", entry = VocabStore.ToDto(existing, includeReviews: false) });
        }

        var entry = new VocabEntry
        {
            Id = Guid.NewGuid(),
            Term = term.Trim(),
            NormalizedTerm = normalized,
            Kind = k,
            Definition = definition,
            Frequency = f,
            Register = r,
            GlossHy = gloss_hy,
            GlossRu = gloss_ru,
            Examples = examples?.ToList() ?? [],
            Collocations = collocations?.ToList() ?? [],
            Synonyms = synonyms?.ToList() ?? [],
            MemoryHook = memory_hook,
            SourceContext = source_context,
            Notes = notes,
            DueOn = VocabStore.Today(),   // a fresh item is due at once
        };
        db.VocabEntries.Add(entry);
        await db.SaveChangesAsync();
        return J(new { status = "created", entry = VocabStore.ToDto(entry, includeReviews: false) });
    }

    [McpServerTool(Name = "record_vocab_review"), Description(
        "Record the outcome of reviewing one vocabulary item and let the server reschedule it (SM-2). " +
        "Grade is 0-5: 0 = no recognition at all; 1 = wrong, but recognised once told; 2 = wrong, though it felt close; " +
        "3 = correct but with real effort; 4 = correct after a hesitation; 5 = instant, perfect, natural use. " +
        "Anything below 3 counts as a failure and sends the item back to a 1-day interval. " +
        "Returns the new interval and the next due date.")]
    public async Task<string> RecordVocabReview(
        [Description("Grade 0-5 (see the tool description)")] int grade,
        [Description("Entry UUID (from get_due_vocab). Preferred.")] Guid? entry_id = null,
        [Description("The term, if you don't have the UUID")] string? term = null,
        [Description("How it was tested, e.g. \"produce\", \"recall\", \"cloze\"")] string? prompt_type = null,
        [Description("What the learner actually answered")] string? answer = null,
        [Description("Why this grade was awarded")] string? note = null)
    {
        if (grade is < 0 or > 5) return J(new { error = "grade must be 0-5" });

        VocabEntry? entry = null;
        if (entry_id is { } id)
            entry = await db.VocabEntries.FirstOrDefaultAsync(v => v.Id == id);
        else if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = VocabStore.Normalize(term);
            entry = await db.VocabEntries.FirstOrDefaultAsync(v => v.NormalizedTerm == normalized);
        }
        else return J(new { error = "supply entry_id or term" });

        if (entry is null) return J(new { error = "entry not found" });

        var review = VocabStore.ApplyReview(entry, grade, prompt_type, answer, note);
        db.VocabReviews.Add(review!);
        await db.SaveChangesAsync();

        return J(new
        {
            status = grade >= Sm2.PassThreshold ? "passed" : "failed",
            term = entry.Term,
            grade,
            interval_days = entry.IntervalDays,
            previous_interval_days = review!.IntervalBefore,
            ease_factor = Math.Round(entry.EaseFactor, 2),
            due_on = entry.DueOn.ToString("yyyy-MM-dd"),
            repetitions = entry.Repetitions,
            lapses = entry.Lapses,
            strength = VocabStore.Strength(entry),
        });
    }

    [McpServerTool(Name = "update_vocab_entry"), Description(
        "Correct or enrich a stored entry. Only the fields you pass are changed; the SM-2 schedule is untouched.")]
    public async Task<string> UpdateVocabEntry(
        [Description("Entry UUID")] Guid entry_id,
        [Description("English definition")] string? definition = null,
        [Description("Word | Idiom | PhrasalVerb | Collocation")] string? kind = null,
        [Description("VeryCommon | Common | Uncommon | Rare")] string? frequency = null,
        [Description("Formal | Neutral | Informal | Slang | Vulgar")] string? register = null,
        [Description("Armenian gloss")] string? gloss_hy = null,
        [Description("Russian gloss")] string? gloss_ru = null,
        [Description("Replaces the example list")] string[]? examples = null,
        [Description("Replaces the collocation list")] string[]? collocations = null,
        [Description("Replaces the synonym list")] string[]? synonyms = null,
        [Description("The mnemonic that makes it stick")] string? memory_hook = null,
        [Description("Anything else worth keeping")] string? notes = null)
    {
        var v = await db.VocabEntries.Include(e => e.Reviews).FirstOrDefaultAsync(e => e.Id == entry_id);
        if (v is null) return J(new { error = "entry not found" });

        if (kind is not null && !TryEnum<VocabKind>(kind, v.Kind, out var k)) return J(new { error = $"invalid kind '{kind}'" });
        else if (kind is not null) v.Kind = Enum.Parse<VocabKind>(kind, true);

        if (frequency is not null && !TryEnum<VocabFrequency>(frequency, v.Frequency, out _)) return J(new { error = $"invalid frequency '{frequency}'" });
        else if (frequency is not null) v.Frequency = Enum.Parse<VocabFrequency>(frequency, true);

        if (register is not null && !TryEnum<VocabRegister>(register, v.Register, out _)) return J(new { error = $"invalid register '{register}'" });
        else if (register is not null) v.Register = Enum.Parse<VocabRegister>(register, true);

        v.Definition = definition ?? v.Definition;
        v.GlossHy = gloss_hy ?? v.GlossHy;
        v.GlossRu = gloss_ru ?? v.GlossRu;
        v.Examples = examples?.ToList() ?? v.Examples;
        v.Collocations = collocations?.ToList() ?? v.Collocations;
        v.Synonyms = synonyms?.ToList() ?? v.Synonyms;
        v.MemoryHook = memory_hook ?? v.MemoryHook;
        v.Notes = notes ?? v.Notes;
        v.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return J(VocabStore.ToDto(v, includeReviews: false));
    }

    [McpServerTool(Name = "delete_vocab_entry"), Description("Permanently delete a vocabulary entry and its whole review history.")]
    public async Task<string> DeleteVocabEntry([Description("Entry UUID")] Guid entry_id)
    {
        var v = await db.VocabEntries.FindAsync(entry_id);
        if (v is null) return J(new { error = "entry not found" });
        db.VocabEntries.Remove(v);
        await db.SaveChangesAsync();
        return J(new { status = "deleted", term = v.Term });
    }

    // ===== Read =====

    [McpServerTool(Name = "get_due_vocab"), Description(
        "The review queue: every item due on or before today, hardest first (lowest ease leads). " +
        "Newly-added items are due immediately. Returns everything needed to quiz the learner — " +
        "definition, examples, memory hook — so no second lookup is needed. Feed the result of each " +
        "question back through record_vocab_review.")]
    public async Task<string> GetDueVocab(
        [Description("Max items to return (default 20)")] int limit = 20,
        [Description("Only this kind: Word | Idiom | PhrasalVerb | Collocation")] string? kind = null)
    {
        if (limit is < 1 or > 200) limit = 20;

        IQueryable<VocabEntry> q = VocabStore.DueQuery(db).AsNoTracking().Include(v => v.Reviews);
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!Enum.TryParse<VocabKind>(kind, true, out var k)) return J(new { error = $"invalid kind '{kind}'" });
            q = q.Where(v => v.Kind == k);
        }

        var due = await q.Take(limit).ToListAsync();
        var total = await VocabStore.DueQuery(db).CountAsync();

        return J(new
        {
            due_count = total,
            returned = due.Count,
            entries = due.Select(v => VocabStore.ToDto(v, includeReviews: false)),
        });
    }

    [McpServerTool(Name = "get_vocab_entry"), Description(
        "Look up one entry in full, including its complete review history (every grade and when). " +
        "Use it to check whether something is already saved before adding it, or to see how a word has been going.")]
    public async Task<string> GetVocabEntry(
        [Description("Entry UUID")] Guid? entry_id = null,
        [Description("The term, if you don't have the UUID")] string? term = null)
    {
        VocabEntry? v = null;
        if (entry_id is { } id)
            v = await db.VocabEntries.AsNoTracking().Include(e => e.Reviews).FirstOrDefaultAsync(e => e.Id == id);
        else if (!string.IsNullOrWhiteSpace(term))
        {
            var normalized = VocabStore.Normalize(term);
            v = await db.VocabEntries.AsNoTracking().Include(e => e.Reviews)
                .FirstOrDefaultAsync(e => e.NormalizedTerm == normalized);
        }
        else return J(new { error = "supply entry_id or term" });

        return v is null ? J(new { found = false }) : J(VocabStore.ToDto(v, includeReviews: true));
    }

    [McpServerTool(Name = "list_vocab_entries"), Description(
        "Browse the vocabulary store. Optionally filter by a substring, by kind, or by strength " +
        "(New | Learning | Young | Mature). Newest first.")]
    public async Task<string> ListVocabEntries(
        [Description("Substring to match against the term or definition")] string? search = null,
        [Description("Word | Idiom | PhrasalVerb | Collocation")] string? kind = null,
        [Description("New | Learning | Young | Mature")] string? strength = null,
        [Description("Max items (default 50)")] int limit = 50)
    {
        if (limit is < 1 or > 500) limit = 50;

        var q = db.VocabEntries.AsNoTracking().Include(v => v.Reviews).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            q = q.Where(v => v.NormalizedTerm.Contains(s) || v.Definition.ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!Enum.TryParse<VocabKind>(kind, true, out var k)) return J(new { error = $"invalid kind '{kind}'" });
            q = q.Where(v => v.Kind == k);
        }

        var list = await q.OrderByDescending(v => v.CreatedAt).ToListAsync();

        // Strength is a derived bucket, so it is filtered in memory rather than in SQL.
        if (!string.IsNullOrWhiteSpace(strength))
            list = list.Where(v => string.Equals(VocabStore.Strength(v), strength, StringComparison.OrdinalIgnoreCase)).ToList();

        return J(new
        {
            total = list.Count,
            entries = list.Take(limit).Select(v => VocabStore.ToDto(v, includeReviews: false)),
        });
    }

    [McpServerTool(Name = "get_vocab_stats"), Description(
        "Totals for the vocabulary store: how many items, how many are due today, the New/Learning/Young/Mature split, review counts and lapses.")]
    public async Task<string> GetVocabStats() => J(await VocabStore.StatsAsync(db));
}
