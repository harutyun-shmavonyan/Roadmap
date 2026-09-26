using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Courses;

public sealed record ExerciseProgress(Guid Id, string Kind, string SectionKind, string Title, bool Required,
    double Weight, double MaxScore, int Attempts, bool HasSubmission, bool HasGrade, double? Fraction);

public sealed record LessonProgress(Guid Id, string Code, string Title, string Status, int Position,
    double Progress, double? Score, double GradedFraction, double SubmittedFraction,
    double? EstimatedHours, bool Defined, List<ExerciseProgress> Exercises);

public sealed record StageProgress(Guid Id, string Code, string Title, string Status, int Position,
    double Progress, double DefinedFraction, int LessonsDefined, int LessonsTotal, double? TargetWeeks,
    List<LessonProgress> Lessons);

public sealed record CourseProgressTree(Guid Id, string Slug, string Title, string Status,
    double Progress, double DefinedFraction, double HoursLogged, double EstimatedHoursRemaining,
    LessonProgress? CurrentLesson, StageProgress? CurrentStage, StageProgress? NextUndefinedStage,
    List<StageProgress> Stages);

/// <summary>
/// The derived half of a course: every number the tab shows and the agent reads, computed from
/// rows rather than stored, so nothing can go stale against the work it describes.
///
/// One pass loads the tree and the events; everything else is arithmetic over that. There is no
/// cache: a course is a few hundred rows, and a wrong cached percentage is worse than a query.
/// </summary>
public static class CourseProgress
{
    /// <summary>
    /// What an exercise is worth, 0..1. <c>best</c> (the default) takes the kindest grade the work
    /// ever received, <c>latest</c> the most recent — set per course in <c>metadata.scoring</c>.
    /// Superseded grades are history and never count either way.
    /// </summary>
    private static double? Fraction(Exercise x, bool latest)
    {
        var superseded = x.Submissions.SelectMany(s => s.Grades)
            .Where(g => g.SupersedesGradeId is not null)
            .Select(g => g.SupersedesGradeId!.Value).ToHashSet();
        var grades = x.Submissions.SelectMany(s => s.Grades)
            .Where(g => !superseded.Contains(g.Id)).ToList();
        if (grades.Count == 0) return null;

        double Norm(Grade g) => g.MaxScore > 0 ? Math.Clamp(g.Score / g.MaxScore, 0, 2) : 0;
        return latest ? Norm(grades.OrderByDescending(g => g.GradedAt).First()) : grades.Max(Norm);
    }

    private static double LessonProgressValue(LessonStatus status, double submitted, double graded) => status switch
    {
        LessonStatus.Placeholder or LessonStatus.Draft or LessonStatus.Ready => 0,
        LessonStatus.InProgress => 0.25 + 0.5 * submitted,
        LessonStatus.Submitted => 0.75 + 0.25 * graded,
        LessonStatus.Completed => 1,
        _ => 0,
    };

    public static bool ScoringIsLatest(Course course)
    {
        try
        {
            using var doc = JsonDocument.Parse(course.Metadata);
            return doc.RootElement.TryGetProperty("scoring", out var s)
                && string.Equals(s.GetString(), "latest", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Load one course whole — stages, lessons, exercises, submissions, grades.</summary>
    public static async Task<Course?> LoadTreeAsync(RoadmapDbContext db, Guid courseId) =>
        await db.Courses.AsNoTracking()
            .Include(c => c.Stages).ThenInclude(s => s.Lessons).ThenInclude(l => l.Exercises)
                .ThenInclude(x => x.Submissions).ThenInclude(s => s.Grades)
            .FirstOrDefaultAsync(c => c.Id == courseId);

    public static async Task<CourseProgressTree> ComputeAsync(RoadmapDbContext db, Guid courseId)
    {
        var course = await LoadTreeAsync(db, courseId) ?? throw CourseException.NotFound("course");
        var latest = ScoringIsLatest(course);

        var hoursLogged = await MinutesLoggedAsync(db, courseId) / 60.0;

        var stages = new List<StageProgress>();
        foreach (var st in course.Stages.OrderBy(s => s.Position))
        {
            var lessons = new List<LessonProgress>();
            foreach (var l in st.Lessons.OrderBy(x => x.Position))
            {
                var exercises = l.Exercises.OrderBy(x => x.Position).Select(x =>
                {
                    var f = Fraction(x, latest);
                    return new ExerciseProgress(x.Id, x.Kind, x.SectionKind, x.Title, x.Required, x.Weight,
                        x.MaxScore, x.Submissions.Count, x.Submissions.Count > 0, f is not null, f);
                }).ToList();

                var required = exercises.Where(x => x.Required).ToList();
                var submittedFraction = required.Count == 0 ? 0 : (double)required.Count(x => x.HasSubmission) / required.Count;
                var gradedFraction = required.Count == 0 ? 0 : (double)required.Count(x => x.HasGrade) / required.Count;

                var graded = required.Where(x => x.Fraction is not null).ToList();
                var weight = graded.Sum(x => x.Weight);
                double? score = graded.Count == 0 || weight <= 0
                    ? null
                    : Math.Round(graded.Sum(x => x.Weight * x.Fraction!.Value) / weight * 100, 1);

                lessons.Add(new LessonProgress(l.Id, l.Code, l.Title, l.Status.ToString(), l.Position,
                    Math.Round(LessonProgressValue(l.Status, submittedFraction, gradedFraction), 4), score,
                    Math.Round(gradedFraction, 4), Math.Round(submittedFraction, 4), l.EstimatedHours,
                    l.Status != LessonStatus.Placeholder, exercises));
            }

            // A placeholder has nothing to be part-done, and a skipped lesson was taken off the
            // table, so neither drags the stage down; both still show in "x of y defined".
            var counted = lessons.Where(x => x.Status is not ("Placeholder" or "Skipped")).ToList();
            var defined = lessons.Count(x => x.Defined);
            stages.Add(new StageProgress(st.Id, st.Code, st.Title, st.Status.ToString(), st.Position,
                counted.Count == 0 ? 0 : Math.Round(counted.Average(x => x.Progress), 4),
                lessons.Count == 0 ? 0 : Math.Round((double)defined / lessons.Count, 4),
                defined, lessons.Count, st.TargetWeeks, lessons));
        }

        var live = stages.Where(s => s.Status != "Skipped").ToList();
        var totalWeight = live.Sum(s => s.TargetWeeks ?? 1);
        var progress = totalWeight <= 0 ? 0
            : Math.Round(live.Sum(s => (s.TargetWeeks ?? 1) * s.Progress) / totalWeight, 4);

        var allLessons = stages.SelectMany(s => s.Lessons).ToList();
        var definedFraction = allLessons.Count == 0 ? 0
            : Math.Round((double)allLessons.Count(l => l.Defined) / allLessons.Count, 4);
        var remaining = stages.SelectMany(s => s.Lessons)
            .Where(l => l.Status is not ("Completed" or "Skipped"))
            .Sum(l => l.EstimatedHours ?? 0);

        // Where to sit down: whatever is already open, else the first thing ready to start.
        var (currentStage, currentLesson) = FirstMatch(stages, l => l.Status is "InProgress" or "Submitted")
            ?? FirstMatch(stages, l => l.Status == "Ready")
            ?? (null, null);

        // And if nothing is ready, the first stage still waiting to be written.
        var nextUndefined = currentLesson is not null ? null
            : stages.FirstOrDefault(s => s.Status == "Planned" || s.Lessons.Any(l => !l.Defined));

        return new CourseProgressTree(course.Id, course.Slug, course.Title, course.Status.ToString(),
            progress, definedFraction, Math.Round(hoursLogged, 2), Math.Round(remaining, 2),
            currentLesson, currentStage, nextUndefined, stages);
    }

    private static (StageProgress, LessonProgress)? FirstMatch(List<StageProgress> stages, Func<LessonProgress, bool> pred)
    {
        foreach (var s in stages.OrderBy(x => x.Position))
            foreach (var l in s.Lessons.OrderBy(x => x.Position))
                if (pred(l)) return (s, l);
        return null;
    }

    /// <summary>Minutes across every <c>time_logged</c> event on the course.</summary>
    public static async Task<double> MinutesLoggedAsync(RoadmapDbContext db, Guid courseId)
    {
        var payloads = await db.ProgressEvents.AsNoTracking()
            .Where(e => e.CourseId == courseId && e.Type == ProgressEventType.TimeLogged)
            .Select(e => e.Payload).ToListAsync();
        double total = 0;
        foreach (var p in payloads)
        {
            try
            {
                using var doc = JsonDocument.Parse(p);
                if (doc.RootElement.TryGetProperty("minutes", out var m) && m.TryGetDouble(out var v)) total += v;
            }
            catch { /* a malformed payload is not worth failing a page load over */ }
        }
        return total;
    }
}
