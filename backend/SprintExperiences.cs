using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// The experiences schedule a sprint must carry (the user's call, 2026-11-16): a sprint cannot be
/// started until every week of it has at least <see cref="PerWeek"/> experiences dated inside it,
/// and Performance and the week view show how many of them were actually had.
///
/// Weeks are the app's Monday–Sunday weeks, clipped to the sprint window — the same weeks the week
/// view shows. A clipped week owes its share, rounded up: ceil(2 × days ÷ 7), so a 4–7 day week
/// owes 2 and a 1–3 day stub owes 1. An experience counts in the week of its start date; status
/// <c>done</c> is "had", <c>planned</c> is "scheduled but not had yet".
///
/// Experiences are global, not roadmap-scoped, so every roadmap's sprints read the same list.
/// Nothing is stored: the schedule is read live from the Experiences tab, so a plan added, moved
/// or ticked done there shows up here at once. REST start, MCP <c>start_sprint</c>, Performance,
/// MCP <c>get_sprint_performance</c> and the week view all go through this one class.
/// </summary>
public static class SprintExperiences
{
    public const int PerWeek = 2;

    public static DateOnly MondayOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public static int RequiredFor(int days) => days <= 0 ? 0 : (int)Math.Ceiling(PerWeek * days / 7.0);

    public static async Task<SprintExperienceScheduleDto> ComputeAsync(RoadmapDbContext db, DateOnly start, DateOnly end)
    {
        var rows = await db.Experiences.AsNoTracking()
            .Where(e => e.StartDate != null && e.StartDate >= start && e.StartDate <= end)
            .OrderBy(e => e.StartDate).ThenBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Title, e.Category, e.Status, Date = e.StartDate!.Value })
            .ToListAsync();

        var weeks = new List<SprintExperienceWeekDto>();
        for (var monday = MondayOf(start); monday <= end; monday = monday.AddDays(7))
        {
            var from = monday < start ? start : monday;
            var sunday = monday.AddDays(6);
            var to = sunday > end ? end : sunday;
            var days = to.DayNumber - from.DayNumber + 1;
            var inWeek = rows.Where(r => r.Date >= from && r.Date <= to)
                .Select(r => new SprintExperienceRefDto(r.Id, r.Title, r.Category,
                    ExperienceLogic.Wire(r.Status), r.Date.ToString("yyyy-MM-dd")))
                .ToList();
            var required = RequiredFor(days);
            weeks.Add(new SprintExperienceWeekDto(
                monday.ToString("yyyy-MM-dd"), from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"),
                days, required, inWeek.Count, inWeek.Count(x => x.Status == "done"),
                inWeek.Count >= required, inWeek));
        }

        return new SprintExperienceScheduleDto(PerWeek,
            weeks.Sum(w => w.Required), weeks.Sum(w => w.Scheduled), weeks.Sum(w => w.Done),
            weeks.All(w => w.Met), weeks);
    }

    /// <summary>The week of <paramref name="anyDay"/>, clipped to the sprint. Null when they do not meet.</summary>
    public static async Task<SprintExperienceWeekDto?> WeekAsync(RoadmapDbContext db, Sprint sprint, DateOnly anyDay)
    {
        var monday = MondayOf(anyDay);
        var from = monday < sprint.StartDate ? sprint.StartDate : monday;
        var to = monday.AddDays(6) > sprint.EndDate ? sprint.EndDate : monday.AddDays(6);
        if (from > to) return null;
        var s = await ComputeAsync(db, from, to);
        return s.Weeks.FirstOrDefault();
    }

    /// <summary>Why a sprint cannot start yet, or null when its schedule is complete.</summary>
    public static string? StartRefusal(SprintExperienceScheduleDto s)
    {
        if (s.Ready) return null;
        var short_ = s.Weeks.Where(w => !w.Met)
            .Select(w => $"{w.From}–{w.To}: {w.Scheduled} of {w.Required}");
        return $"Schedule experiences before starting the sprint — at least {PerWeek} a week, dated in the " +
               $"Experiences tab. Short: {string.Join("; ", short_)}.";
    }
}
