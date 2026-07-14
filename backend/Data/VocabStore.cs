using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Data;

/// <summary>
/// The single home for vocabulary rules — normalisation, SM-2 application, strength
/// bucketing and DTO shaping. Both the REST endpoints (which feed the English tab) and
/// the MCP tools (which feed the chat-driven training) go through here, so the two
/// surfaces can never drift apart on what "due" or "mature" means.
/// </summary>
public static class VocabStore
{
    /// <summary>Yerevan, matching the daily-notes convention — "today" must mean the learner's today.</summary>
    private static readonly TimeZoneInfo Tz = ResolveYerevan();

    private static TimeZoneInfo ResolveYerevan()
    {
        foreach (var id in new[] { "Asia/Yerevan", "Caucasus Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { /* try the next id */ }
            catch (InvalidTimeZoneException) { /* try the next id */ }
        }
        return TimeZoneInfo.Utc;
    }

    public static DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz));

    public static string Normalize(string term) => term.Trim().ToLowerInvariant();

    /// <summary>
    /// How well-established an item is. Mirrors the usual SRS buckets so the tab can show
    /// progress at a glance: never reviewed → still bedding in → holding → durable.
    /// </summary>
    public static string Strength(VocabEntry v) => v.TotalReviews == 0
        ? "New"
        : v.IntervalDays < 7 ? "Learning"
        : v.IntervalDays < 21 ? "Young"
        : "Mature";

    public static VocabEntryDto ToDto(VocabEntry v, bool includeReviews)
    {
        var today = Today();
        return new VocabEntryDto(
            v.Id, v.Term, v.Kind.ToString(), v.Definition, v.GlossHy, v.GlossRu,
            v.Frequency.ToString(), v.Register.ToString(),
            v.Examples, v.Collocations, v.Synonyms,
            v.MemoryHook, v.SourceContext, v.Notes,
            v.Repetitions, Math.Round(v.EaseFactor, 2), v.IntervalDays,
            v.DueOn.ToString("yyyy-MM-dd"), v.LastReviewedAt,
            v.Lapses, v.TotalReviews, Strength(v), v.DueOn <= today, v.CreatedAt,
            includeReviews
                ? v.Reviews.OrderByDescending(r => r.ReviewedAt)
                    .Select(r => new VocabReviewDto(r.ReviewedAt, r.Grade, r.PromptType, r.Answer, r.Note,
                        r.IntervalBefore, r.IntervalAfter, Math.Round(r.EaseBefore, 2), Math.Round(r.EaseAfter, 2)))
                    .ToList()
                : []);
    }

    /// <summary>
    /// Apply a graded review: advance (or collapse) the SM-2 state, stamp the next due date,
    /// and build the immutable <see cref="VocabReview"/> row for it. Returns null if the entry is gone.
    ///
    /// The returned review is NOT attached — the caller must <c>db.VocabReviews.Add(review)</c>.
    /// Attaching it through <c>v.Reviews</c> instead would make EF treat a row whose Guid key is
    /// already set as Modified rather than Added, and it would emit an UPDATE against a row that
    /// does not exist yet (DbUpdateConcurrencyException, 0 rows affected).
    /// Neither does this save — the caller owns the transaction boundary.
    /// </summary>
    public static VocabReview? ApplyReview(VocabEntry? v, int grade, string? promptType, string? answer, string? note)
    {
        if (v is null) return null;

        var before = new Sm2.State(v.Repetitions, v.EaseFactor, v.IntervalDays);
        var after = Sm2.Next(before, grade);

        var review = new VocabReview
        {
            Id = Guid.NewGuid(),
            VocabEntryId = v.Id,
            ReviewedAt = DateTime.UtcNow,
            Grade = Math.Clamp(grade, 0, 5),
            PromptType = promptType,
            Answer = answer,
            Note = note,
            IntervalBefore = before.IntervalDays,
            IntervalAfter = after.IntervalDays,
            EaseBefore = before.EaseFactor,
            EaseAfter = after.EaseFactor,
        };

        // A lapse is only a lapse if there was something to lose — failing a brand-new
        // item is just learning, not forgetting.
        if (grade < Sm2.PassThreshold && before.Repetitions > 0) v.Lapses++;

        v.Repetitions = after.Repetitions;
        v.EaseFactor = after.EaseFactor;
        v.IntervalDays = after.IntervalDays;
        v.DueOn = Today().AddDays(after.IntervalDays);
        v.LastReviewedAt = review.ReviewedAt;
        v.TotalReviews++;
        v.UpdatedAt = DateTime.UtcNow;

        return review;
    }

    /// <summary>Everything due on or before today, hardest-first (weakest ease leads), then oldest-due.</summary>
    public static IQueryable<VocabEntry> DueQuery(RoadmapDbContext db) =>
        db.VocabEntries.Where(v => v.DueOn <= Today())
            .OrderBy(v => v.EaseFactor).ThenBy(v => v.DueOn);

    public static async Task<VocabStatsDto> StatsAsync(RoadmapDbContext db)
    {
        var today = Today();
        var all = await db.VocabEntries.AsNoTracking().ToListAsync();
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var reviewsAll = await db.VocabReviews.CountAsync();
        var reviews7 = await db.VocabReviews.CountAsync(r => r.ReviewedAt >= weekAgo);

        return new VocabStatsDto(
            Total: all.Count,
            DueToday: all.Count(v => v.DueOn <= today),
            New: all.Count(v => Strength(v) == "New"),
            Learning: all.Count(v => Strength(v) == "Learning"),
            Young: all.Count(v => Strength(v) == "Young"),
            Mature: all.Count(v => Strength(v) == "Mature"),
            ReviewsAllTime: reviewsAll,
            ReviewsLast7Days: reviews7,
            AverageEase: all.Count == 0 ? 0 : Math.Round(all.Average(v => v.EaseFactor), 2),
            Lapses: all.Sum(v => v.Lapses));
    }
}
