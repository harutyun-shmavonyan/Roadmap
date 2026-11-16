using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// The network schedule a sprint must carry (the user's call, 2026-11-16): a new person to meet every
/// week. The people are the tasks of the roadmap's <b>Network</b> task project (matched by name,
/// case-insensitively — <see cref="ProjectName"/>); one task is one person. A person counts in the
/// week its <c>DueDate</c> falls in — that date is the meeting — and is "met" once the task is
/// completed.
///
/// Same weeks and the same shape as <see cref="SprintExperiences"/> (Monday–Sunday, clipped to the
/// sprint; a clipped week owes ceil(1 × days ÷ 7), i.e. 1), and the same DTOs, so the sprint modal,
/// Performance and the week view draw both sections with one component. Nothing is stored; it reads
/// the Tasks tab live. No points of its own: a completed task already earns its task points.
/// </summary>
public static class SprintNetwork
{
    public const int PerWeek = 1;
    public const string ProjectName = "Network";

    public static async Task<SprintExperienceScheduleDto> ComputeAsync(RoadmapDbContext db, Guid roadmapId, DateOnly start, DateOnly end)
    {
        var project = await TaskLogic.FindProjectByNameAsync(db, roadmapId, ProjectName);
        var projectId = project?.Id ?? Guid.Empty; // no Network project yet → nothing matches
        var rows = await db.SingleTasks.AsNoTracking()
            .Where(t => t.RoadmapId == roadmapId && t.ProjectId == projectId
                && t.DueDate != null && t.DueDate >= start && t.DueDate <= end)
            .OrderBy(t => t.DueDate).ThenBy(t => t.CreatedAt)
            .Select(t => new { t.Id, t.Title, t.IsCompleted, Date = t.DueDate!.Value })
            .ToListAsync();

        var weeks = new List<SprintExperienceWeekDto>();
        for (var monday = SprintExperiences.MondayOf(start); monday <= end; monday = monday.AddDays(7))
        {
            var from = monday < start ? start : monday;
            var sunday = monday.AddDays(6);
            var to = sunday > end ? end : sunday;
            var days = to.DayNumber - from.DayNumber + 1;
            var inWeek = rows.Where(r => r.Date >= from && r.Date <= to)
                .Select(r => new SprintExperienceRefDto(r.Id, r.Title, null, r.IsCompleted ? "done" : "planned",
                    r.Date.ToString("yyyy-MM-dd")))
                .ToList();
            var required = days <= 0 ? 0 : (int)Math.Ceiling(PerWeek * days / 7.0);
            weeks.Add(new SprintExperienceWeekDto(
                monday.ToString("yyyy-MM-dd"), from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"),
                days, required, inWeek.Count, inWeek.Count(x => x.Status == "done"),
                inWeek.Count >= required, inWeek));
        }

        return new SprintExperienceScheduleDto(PerWeek,
            weeks.Sum(w => w.Required), weeks.Sum(w => w.Scheduled), weeks.Sum(w => w.Done),
            weeks.All(w => w.Met), weeks);
    }

    public static async Task<SprintExperienceWeekDto?> WeekAsync(RoadmapDbContext db, Sprint sprint, DateOnly anyDay)
    {
        var monday = SprintExperiences.MondayOf(anyDay);
        var from = monday < sprint.StartDate ? sprint.StartDate : monday;
        var to = monday.AddDays(6) > sprint.EndDate ? sprint.EndDate : monday.AddDays(6);
        if (from > to) return null;
        return (await ComputeAsync(db, sprint.RoadmapId, from, to)).Weeks.FirstOrDefault();
    }

    public static string? StartRefusal(SprintExperienceScheduleDto s)
    {
        if (s.Ready) return null;
        var short_ = s.Weeks.Where(w => !w.Met).Select(w => $"{w.From}–{w.To}: {w.Scheduled} of {w.Required}");
        return $"Schedule your network before starting the sprint — a new person to meet each week (a task in the " +
               $"Tasks tab's \"{ProjectName}\" project, due that week). Short: {string.Join("; ", short_)}.";
    }

    /// <summary>
    /// Habits a sprint must track before it starts (the user's call, 2026-11-17): at least this many
    /// habits linked to the sprint (Habits tab / the sprint modal), paused ones not counted.
    /// </summary>
    public const int MinHabits = 1;

    /// <summary>Every check Start Sprint makes, in one place — REST and MCP both call this.</summary>
    public static async Task<(string? Refusal, SprintExperienceScheduleDto Experiences, SprintExperienceScheduleDto Network, int Habits)>
        StartChecksAsync(RoadmapDbContext db, Sprint sprint)
    {
        var exp = await SprintExperiences.ComputeAsync(db, sprint.StartDate, sprint.EndDate);
        var net = await ComputeAsync(db, sprint.RoadmapId, sprint.StartDate, sprint.EndDate);
        var habits = await db.SprintHabits.CountAsync(h => h.SprintId == sprint.Id && !h.IsPaused);
        var habitRefusal = habits >= MinHabits ? null
            : $"Choose the habits to track before starting the sprint — at least {MinHabits} (it has {habits}).";
        var reasons = new[] { SprintExperiences.StartRefusal(exp), StartRefusal(net), habitRefusal }
            .Where(r => r is not null).ToList();
        return (reasons.Count == 0 ? null : string.Join(" ", reasons), exp, net, habits);
    }
}
