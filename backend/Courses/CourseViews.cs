using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Courses;

/// <summary>
/// The read shapes, in one place, because two surfaces serve them: the REST endpoints behind the
/// Courses tab and the MCP tools the authoring agent reads. The tab and the agent should never be
/// looking at differently-shaped versions of the same course, and the only way to guarantee that
/// is for neither of them to own the projection.
///
/// Statuses are written out with <see cref="CourseLogic.Wire"/> rather than left as enums: the REST
/// pipeline has no snake_case enum converter (adding one would change every other endpoint's
/// output), so the conversion belongs here, where both callers get it.
/// </summary>
public static class CourseViews
{
    /// <summary>The course list: enough for a card, no tree.</summary>
    public static async Task<List<object>> SummariesAsync(RoadmapDbContext db, CourseStatus? status)
    {
        var q = db.Courses.AsNoTracking();
        if (status is CourseStatus st) q = q.Where(c => c.Status == st);
        var courses = await q.OrderByDescending(c => c.UpdatedAt).ToListAsync();

        var list = new List<object>();
        foreach (var c in courses)
        {
            var p = await CourseProgress.ComputeAsync(db, c.Id);
            list.Add(new
            {
                id = c.Id, slug = c.Slug, title = c.Title, subtitle = c.Subtitle, status = c.Status.Wire(),
                progress = p.Progress, definedFraction = p.DefinedFraction, hoursLogged = p.HoursLogged,
                estimatedHoursRemaining = p.EstimatedHoursRemaining,
                currentLesson = p.CurrentLesson is null ? null : new
                {
                    code = p.CurrentLesson.Code, title = p.CurrentLesson.Title,
                    status = p.CurrentLesson.Status.Wire(),
                },
                updatedAt = c.UpdatedAt,
            });
        }
        return list;
    }

    /// <summary>
    /// A course as a tree. <paramref name="depth"/> is 'stages', 'lessons' or 'full'; 'full' carries
    /// every section's Markdown and every exercise's prompt, but never submissions — a lesson's
    /// actual work is heavy and belongs to <see cref="LessonDetailAsync"/>.
    /// </summary>
    public static async Task<object> DetailAsync(RoadmapDbContext db, Course course, string depth)
    {
        var template = await CourseLogic.TemplateAsync(db, course.TemplateId);
        var tree = await CourseProgress.ComputeAsync(db, course.Id);
        var full = depth == "full";
        var withLessons = full || depth == "lessons";

        var sections = full
            ? await db.LessonSections.AsNoTracking().Where(s => s.Lesson.Stage.CourseId == course.Id).ToListAsync()
            : [];
        var exercises = full
            ? await db.Exercises.AsNoTracking().Where(x => x.Lesson.Stage.CourseId == course.Id).ToListAsync()
            : [];

        return new
        {
            id = course.Id, slug = course.Slug, title = course.Title, subtitle = course.Subtitle,
            status = course.Status.Wire(), descriptionMd = course.DescriptionMd,
            capstoneMd = course.CapstoneMd, targetHoursPerWeek = course.TargetHoursPerWeek,
            metadata = JsonSerializer.Deserialize<JsonElement>(course.Metadata),
            template = new
            {
                id = template.Id, name = template.Name,
                sections = JsonSerializer.Deserialize<JsonElement>(template.Sections),
                exerciseKinds = JsonSerializer.Deserialize<JsonElement>(template.ExerciseKinds),
            },
            progress = tree.Progress, definedFraction = tree.DefinedFraction,
            hoursLogged = tree.HoursLogged, estimatedHoursRemaining = tree.EstimatedHoursRemaining,
            startedAt = course.StartedAt, completedAt = course.CompletedAt, updatedAt = course.UpdatedAt,
            stages = tree.Stages.Select(s => new
            {
                id = s.Id, code = s.Code, title = s.Title, status = s.Status.Wire(), position = s.Position,
                targetWeeks = s.TargetWeeks, progress = s.Progress, definedFraction = s.DefinedFraction,
                lessonsDefined = s.LessonsDefined, lessonsTotal = s.LessonsTotal,
                lessons = withLessons ? s.Lessons.Select(l => new
                {
                    id = l.Id, code = l.Code, title = l.Title, status = l.Status.Wire(),
                    position = l.Position, progress = l.Progress, score = l.Score,
                    estimatedHours = l.EstimatedHours,
                    exerciseCount = l.Exercises.Count,
                    sections = full
                        ? sections.Where(x => x.LessonId == l.Id).OrderBy(x => x.Position)
                            .Select(x => new { kind = x.Kind, title = x.Title, contentMd = x.ContentMd })
                            .ToList<object>()
                        : null,
                    exercises = full
                        ? exercises.Where(x => x.LessonId == l.Id).OrderBy(x => x.Position)
                            .Select(x => new
                            {
                                id = x.Id, kind = x.Kind, sectionKind = x.SectionKind, title = x.Title,
                                promptMd = x.PromptMd, referenceMd = x.ReferenceMd,
                                maxScore = x.MaxScore, weight = x.Weight, required = x.Required,
                            }).ToList<object>()
                        : null,
                }).ToList<object>() : null,
            }).ToList(),
            resources = await db.CourseResources.AsNoTracking()
                .Where(r => r.CourseId == course.Id && r.LessonId == null)
                .OrderBy(r => r.Position)
                .Select(r => new { id = r.Id, kind = r.Kind, title = r.Title, url = r.Url, noteMd = r.NoteMd })
                .ToListAsync(),
        };
    }

    /// <summary>
    /// One lesson in full. <paramref name="include"/> holds any of sections, exercises, submissions,
    /// grades; what is not asked for comes back null rather than empty, so an omitted include never
    /// reads as "this lesson has none".
    /// </summary>
    public static async Task<object> LessonDetailAsync(RoadmapDbContext db, Course course, Lesson lesson,
        HashSet<string> include)
    {
        var template = await CourseLogic.TemplateAsync(db, course.TemplateId);
        var order = CourseLogic.Sections(template).Select((s, i) => (s.Kind, i))
            .ToDictionary(x => x.Kind, x => x.i);

        var sections = include.Contains("sections")
            ? (await db.LessonSections.AsNoTracking().Where(s => s.LessonId == lesson.Id).ToListAsync())
                .OrderBy(s => order.GetValueOrDefault(s.Kind, 999))
                .Select(s => (object)new { kind = s.Kind, title = s.Title, contentMd = s.ContentMd }).ToList()
            : null;

        List<object>? exercises = null;
        if (include.Contains("exercises"))
        {
            exercises = [];
            var rows = await db.Exercises.AsNoTracking()
                .Include(x => x.Submissions).ThenInclude(s => s.Grades)
                .Where(x => x.LessonId == lesson.Id).OrderBy(x => x.Position).ToListAsync();
            foreach (var x in rows)
                exercises.Add(new
                {
                    id = x.Id, kind = x.Kind, sectionKind = x.SectionKind, title = x.Title,
                    promptMd = x.PromptMd, referenceMd = x.ReferenceMd, maxScore = x.MaxScore,
                    weight = x.Weight, required = x.Required, attempts = x.Submissions.Count,
                    submissions = include.Contains("submissions")
                        ? x.Submissions.OrderBy(s => s.AttemptNo).Select(s => new
                        {
                            id = s.Id, attemptNo = s.AttemptNo, contentMd = s.ContentMd,
                            links = JsonSerializer.Deserialize<JsonElement>(s.Links),
                            submittedBy = s.SubmittedBy, submittedAt = s.SubmittedAt,
                            grades = include.Contains("grades")
                                ? s.Grades.OrderByDescending(g => g.GradedAt).Select(g => new
                                {
                                    id = g.Id, score = g.Score, maxScore = g.MaxScore,
                                    feedbackMd = g.FeedbackMd, gradedBy = g.GradedBy,
                                    gradedAt = g.GradedAt, supersedes = g.SupersedesGradeId,
                                }).ToList<object>() : null,
                        }).ToList<object>() : null,
                });
        }

        var tree = await CourseProgress.ComputeAsync(db, course.Id);
        var lp = tree.Stages.SelectMany(s => s.Lessons).FirstOrDefault(l => l.Id == lesson.Id);
        var stage = await db.Stages.AsNoTracking().FirstAsync(s => s.Id == lesson.StageId);

        return new
        {
            id = lesson.Id, code = lesson.Code, title = lesson.Title, status = lesson.Status.Wire(),
            summaryMd = lesson.SummaryMd, estimatedHours = lesson.EstimatedHours,
            stage = new { id = stage.Id, code = stage.Code, title = stage.Title },
            progress = lp?.Progress, score = lp?.Score,
            gradedFraction = lp?.GradedFraction, submittedFraction = lp?.SubmittedFraction,
            // The template's sections, not the lesson's: the tab renders the whole rhythm and shows
            // which steps are still blank, so a half-written lesson looks unfinished rather than short.
            templateSections = CourseLogic.Sections(template)
                .Select(s => new { kind = s.Kind, title = s.Title, required = s.Required }),
            sections, exercises,
            resources = await db.CourseResources.AsNoTracking()
                .Where(r => r.LessonId == lesson.Id).OrderBy(r => r.Position)
                .Select(r => new { id = r.Id, kind = r.Kind, title = r.Title, url = r.Url, noteMd = r.NoteMd })
                .ToListAsync(),
        };
    }

    /// <summary>The default include set for a lesson read: everything the reader needs at once.</summary>
    public static HashSet<string> LessonInclude(string[]? include) =>
        (include is { Length: > 0 } ? include : ["sections", "exercises", "submissions", "grades"])
            .Select(s => s.ToLowerInvariant()).ToHashSet();
}
