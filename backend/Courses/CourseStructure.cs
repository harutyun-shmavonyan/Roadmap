using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Courses;

/// <summary>
/// Building and rearranging a course: stages, lessons, sections, exercises. Every entry point
/// writes a <see cref="ProgressEvent"/> and leaves positions dense, so a caller cannot produce a
/// course whose order or history is half-written.
///
/// Creates keyed on a natural key (a stage <c>code</c>, a lesson <c>code</c>, a section
/// <c>kind</c>) return what is already there rather than failing, because the agent calling them
/// retries and a retry must not be an error.
/// </summary>
public static class CourseStructure
{
    // ===== Stages =====

    public static async Task<(Stage Stage, bool Created)> CreateStageAsync(RoadmapDbContext db, Course course,
        string code, string title, string? summaryMd, double? targetWeeks, int? position, string actor)
    {
        var existing = await db.Stages.FirstOrDefaultAsync(s => s.CourseId == course.Id && s.Code == code);
        if (existing is not null) return (existing, false);

        var max = await db.Stages.Where(s => s.CourseId == course.Id)
            .Select(s => (int?)s.Position).MaxAsync() ?? -1;
        var stage = new Stage
        {
            Id = Guid.NewGuid(), CourseId = course.Id, Code = code, Title = title,
            SummaryMd = CourseLogic.Md(summaryMd, "summaryMd"), TargetWeeks = targetWeeks,
            Position = position ?? max + 1,
        };
        db.Stages.Add(stage);
        if (position is int p) await ShiftStagesAsync(db, course.Id, p, stage.Id);
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
            new { summary = $"stage {code} created" }, stage.Id);
        return (stage, true);
    }

    private static async Task ShiftStagesAsync(RoadmapDbContext db, Guid courseId, int from, Guid except)
    {
        var after = await db.Stages.Where(s => s.CourseId == courseId && s.Id != except && s.Position >= from)
            .OrderBy(s => s.Position).ToListAsync();
        var next = from + 1;
        foreach (var s in after) s.Position = next++;
    }

    public static async Task ReorderStagesAsync(RoadmapDbContext db, Course course, List<string> codesInOrder, string actor)
    {
        var stages = await db.Stages.Where(s => s.CourseId == course.Id).ToListAsync();
        var missing = codesInOrder.Where(c => stages.All(s => s.Code != c)).ToList();
        if (missing.Count > 0) throw CourseException.Validation([.. missing.Select(c => $"unknown stage code {c}")]);

        // Anything the caller left out keeps its relative order behind the ones it named.
        var ordered = codesInOrder.Select(c => stages.First(s => s.Code == c))
            .Concat(stages.Where(s => !codesInOrder.Contains(s.Code)).OrderBy(s => s.Position)).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i].Position = i;
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
            new { summary = "stages reordered", order = ordered.Select(s => s.Code) });
    }

    public static async Task DeleteStageAsync(RoadmapDbContext db, Course course, Stage stage, bool cascade, string actor)
    {
        var withWork = await db.Lessons.Where(l => l.StageId == stage.Id)
            .AnyAsync(l => l.Exercises.Any(x => x.Submissions.Any()));
        if (withWork && !cascade)
            throw CourseException.Validation("Stage has lessons with submissions; pass cascade to delete anyway.");

        var now = DateTime.UtcNow;
        stage.DeletedAt = now;
        foreach (var l in await db.Lessons.Where(l => l.StageId == stage.Id).ToListAsync()) l.DeletedAt = now;
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
            new { summary = $"stage {stage.Code} deleted" }, stage.Id);
    }

    // ===== Lessons =====

    public static async Task<(Lesson Lesson, bool Created)> CreateLessonAsync(RoadmapDbContext db, Course course,
        Stage stage, string code, string title, string? summaryMd, double? estimatedHours,
        LessonStatus? status, int? position, string actor)
    {
        // Codes are unique per course, so a repeat anywhere in the course is the same lesson.
        var existing = await db.Lessons.FirstOrDefaultAsync(l => l.Code == code && l.Stage.CourseId == course.Id);
        if (existing is not null) return (existing, false);

        var max = await db.Lessons.Where(l => l.StageId == stage.Id).Select(l => (int?)l.Position).MaxAsync() ?? -1;
        var lesson = new Lesson
        {
            Id = Guid.NewGuid(), StageId = stage.Id, Code = code, Title = title,
            SummaryMd = CourseLogic.Md(summaryMd, "summaryMd"), EstimatedHours = estimatedHours,
            Status = status ?? LessonStatus.Placeholder, Position = position ?? max + 1,
        };
        db.Lessons.Add(lesson);
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
            new { summary = $"lesson {code} created" }, stage.Id, lesson.Id);
        return (lesson, true);
    }

    public static async Task ReorderLessonsAsync(RoadmapDbContext db, Course course, Stage stage,
        List<string> codesInOrder, string actor)
    {
        var lessons = await db.Lessons.Where(l => l.StageId == stage.Id).ToListAsync();
        var missing = codesInOrder.Where(c => lessons.All(l => l.Code != c)).ToList();
        if (missing.Count > 0) throw CourseException.Validation([.. missing.Select(c => $"unknown lesson code {c}")]);

        var ordered = codesInOrder.Select(c => lessons.First(l => l.Code == c))
            .Concat(lessons.Where(l => !codesInOrder.Contains(l.Code)).OrderBy(l => l.Position)).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i].Position = i;
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
            new { summary = $"lessons of {stage.Code} reordered", order = ordered.Select(l => l.Code) }, stage.Id);
    }

    public static async Task MoveLessonAsync(RoadmapDbContext db, Course course, Lesson lesson, Stage toStage,
        int? position, string actor)
    {
        var fromStageId = lesson.StageId;
        var max = await db.Lessons.Where(l => l.StageId == toStage.Id && l.Id != lesson.Id)
            .Select(l => (int?)l.Position).MaxAsync() ?? -1;
        lesson.StageId = toStage.Id;
        lesson.Position = position ?? max + 1;
        lesson.UpdatedAt = DateTime.UtcNow;

        // Close the gap the lesson left behind so both stages stay dense.
        var left = await db.Lessons.Where(l => l.StageId == fromStageId).OrderBy(l => l.Position).ToListAsync();
        for (var i = 0; i < left.Count; i++) left[i].Position = i;

        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
            new { summary = $"lesson {lesson.Code} moved to {toStage.Code}" }, toStage.Id, lesson.Id);
    }

    public static async Task DeleteLessonAsync(RoadmapDbContext db, Course course, Lesson lesson, bool force, string actor)
    {
        var hasWork = await db.Exercises.Where(x => x.LessonId == lesson.Id).AnyAsync(x => x.Submissions.Any());
        if (hasWork && !force)
            throw CourseException.Validation("Lesson has submissions; pass force to delete anyway.");
        lesson.DeletedAt = DateTime.UtcNow;
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
            new { summary = $"lesson {lesson.Code} deleted" }, lesson.StageId, lesson.Id);
    }

    // ===== Sections =====

    /// <summary>Idempotent on (lesson, kind) — that pair is a unique index, and this is why.</summary>
    public static async Task<(LessonSection Section, bool Created)> UpsertSectionAsync(RoadmapDbContext db,
        Course course, LessonTemplate template, Lesson lesson, string kind, string? title, string contentMd, string actor)
    {
        var known = CourseLogic.Sections(template);
        var spec = known.FirstOrDefault(s => s.Kind == kind);
        if (spec is null)
            throw new CourseException("template", new
            {
                error = "template_violation",
                detail = $"Section kind '{kind}' is not in template '{template.Name}'.",
                allowedSectionKinds = known.Select(s => s.Kind).ToArray(),
            });

        CourseLogic.Md(contentMd, "contentMd");
        var position = known.FindIndex(s => s.Kind == kind);
        var existing = await db.LessonSections.FirstOrDefaultAsync(s => s.LessonId == lesson.Id && s.Kind == kind);
        if (existing is not null)
        {
            // Writing the same Markdown back is not an edit: the row keeps its UpdatedAt and the
            // timeline stays a list of changes rather than of calls.
            var changed = existing.ContentMd != contentMd || existing.Position != position
                || (title is not null && existing.Title != title);
            if (changed)
            {
                existing.Title = title ?? existing.Title;
                existing.ContentMd = contentMd;
                existing.Position = position;
                existing.UpdatedAt = DateTime.UtcNow;
                CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
                    new { summary = $"section {kind} of {lesson.Code} updated" }, lesson.StageId, lesson.Id);
            }
            return (existing, false);
        }

        var section = new LessonSection
        {
            Id = Guid.NewGuid(), LessonId = lesson.Id, Kind = kind,
            Title = title ?? spec.Title, ContentMd = contentMd, Position = position,
        };
        db.LessonSections.Add(section);
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
            new { summary = $"section {kind} added to {lesson.Code}" }, lesson.StageId, lesson.Id);
        return (section, true);
    }

    // ===== Exercises =====

    public static async Task<(Exercise Exercise, bool Created)> UpsertExerciseAsync(RoadmapDbContext db,
        Course course, LessonTemplate template, Lesson lesson, string kind, string title, string promptMd,
        string? referenceMd, double? maxScore, double? weight, bool? required, int? position, string actor)
    {
        var kinds = CourseLogic.ExerciseKinds(template);
        var spec = kinds.FirstOrDefault(k => k.Kind == kind);
        if (spec is null)
            throw new CourseException("template", new
            {
                error = "template_violation",
                detail = $"Exercise kind '{kind}' is not in template '{template.Name}'.",
                allowedExerciseKinds = kinds.Select(k => k.Kind).ToArray(),
            });

        CourseLogic.Md(promptMd, "promptMd");
        CourseLogic.Md(referenceMd, "referenceMd");

        // Natural key is (lesson, kind, title): a lesson may hold several comprehension questions.
        var existing = await db.Exercises
            .FirstOrDefaultAsync(x => x.LessonId == lesson.Id && x.Kind == kind && x.Title == title);
        if (existing is not null)
        {
            // Only write what actually differs. Re-importing the same outline must leave the row
            // alone, or UpdatedAt stops meaning "when this last changed".
            var changed = false;
            if (existing.PromptMd != promptMd) { existing.PromptMd = promptMd; changed = true; }
            if (referenceMd is not null && existing.ReferenceMd != referenceMd)
            { existing.ReferenceMd = referenceMd; changed = true; }
            if (maxScore is double ms && existing.MaxScore != ms) { existing.MaxScore = ms; changed = true; }
            if (weight is double w && existing.Weight != w) { existing.Weight = w; changed = true; }
            if (required is bool rq && existing.Required != rq) { existing.Required = rq; changed = true; }
            if (changed) existing.UpdatedAt = DateTime.UtcNow;
            return (existing, false);
        }

        var max = await db.Exercises.Where(x => x.LessonId == lesson.Id).Select(x => (int?)x.Position).MaxAsync() ?? -1;
        var exercise = new Exercise
        {
            Id = Guid.NewGuid(), LessonId = lesson.Id, Kind = kind, Title = title, PromptMd = promptMd,
            ReferenceMd = referenceMd, SectionKind = spec.SectionKind ?? kind,
            MaxScore = maxScore ?? spec.DefaultMaxScore, Weight = weight ?? spec.DefaultWeight,
            Required = required ?? true, Position = position ?? max + 1,
        };
        db.Exercises.Add(exercise);
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
            new { summary = $"exercise '{title}' added to {lesson.Code}" }, lesson.StageId, lesson.Id, exercise.Id);
        return (exercise, true);
    }

    public static async Task DeleteExerciseAsync(RoadmapDbContext db, Course course, Exercise exercise, bool force, string actor)
    {
        var hasWork = await db.Submissions.AnyAsync(s => s.ExerciseId == exercise.Id);
        if (hasWork && !force)
            throw CourseException.Validation("Exercise has submissions; pass force to delete anyway.");
        exercise.DeletedAt = DateTime.UtcNow;
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor,
            new { summary = $"exercise '{exercise.Title}' deleted" }, null, exercise.LessonId, exercise.Id);
    }

    // ===== Status =====

    public static async Task SetLessonStatusAsync(RoadmapDbContext db, Course course, LessonTemplate template,
        Lesson lesson, LessonStatus to, bool force, string actor)
    {
        CourseLogic.CheckLessonMove(lesson.Status, to);
        if (lesson.Status == to) return;
        await CourseLogic.GuardLessonTargetAsync(db, lesson, template, to, force);

        var from = lesson.Status;
        lesson.Status = to;
        lesson.UpdatedAt = DateTime.UtcNow;
        CourseLogic.Event(db, course.Id, ProgressEventType.StatusChanged, actor,
            new { from = from.Wire(), to = to.Wire(), forced = force ? true : (bool?)null },
            lesson.StageId, lesson.Id);
    }

    public static void SetStageStatus(RoadmapDbContext db, Course course, Stage stage, StageStatus to, string actor)
    {
        CourseLogic.CheckStageMove(stage.Status, to);
        if (stage.Status == to) return;
        var from = stage.Status;
        stage.Status = to;
        stage.UpdatedAt = DateTime.UtcNow;
        CourseLogic.Event(db, course.Id, ProgressEventType.StatusChanged, actor,
            new { from = from.Wire(), to = to.Wire() }, stage.Id);
    }

    public static void SetCourseStatus(RoadmapDbContext db, Course course, CourseStatus to, string actor)
    {
        CourseLogic.CheckCourseMove(course.Status, to);
        if (course.Status == to) return;
        var from = course.Status;
        course.Status = to;
        course.UpdatedAt = DateTime.UtcNow;
        if (to == CourseStatus.Active && course.StartedAt is null) course.StartedAt = DateTime.UtcNow;
        if (to == CourseStatus.Completed) course.CompletedAt = DateTime.UtcNow;
        CourseLogic.Event(db, course.Id, ProgressEventType.StatusChanged, actor,
            new { from = from.Wire(), to = to.Wire() });
    }
}
