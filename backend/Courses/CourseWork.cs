using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Courses;

/// <summary>
/// Submitting work, grading it, and answering "where were we". The three things a study session
/// starts and ends with.
/// </summary>
public static class CourseWork
{
    /// <summary>
    /// Append an attempt. Never an edit: a second try is a second row, so the record of how the
    /// work went is not rewritten by how it ended. Submitting on a ready lesson starts it, and
    /// submitting the last required thing marks the lesson submitted — see
    /// <see cref="CourseLogic.CascadeAsync"/>.
    /// </summary>
    public static async Task<Submission> AddSubmissionAsync(RoadmapDbContext db, Course course, Exercise exercise,
        string? contentMd, object? links, string submittedBy, string? idempotencyKey)
    {
        if (idempotencyKey is not null)
        {
            var seen = await db.Submissions
                .FirstOrDefaultAsync(s => s.ExerciseId == exercise.Id && s.IdempotencyKey == idempotencyKey);
            if (seen is not null) return seen;
        }

        CourseLogic.Md(contentMd, "contentMd");
        var next = (await db.Submissions.Where(s => s.ExerciseId == exercise.Id)
            .Select(s => (int?)s.AttemptNo).MaxAsync() ?? 0) + 1;

        var submission = new Submission
        {
            Id = Guid.NewGuid(), ExerciseId = exercise.Id, AttemptNo = next, ContentMd = contentMd,
            Links = CourseLogic.LinksJson(links), SubmittedBy = submittedBy, IdempotencyKey = idempotencyKey,
        };
        db.Submissions.Add(submission);

        var lesson = await db.Lessons.FirstOrDefaultAsync(l => l.Id == exercise.LessonId)
            ?? throw CourseException.NotFound("lesson");
        CourseLogic.Event(db, course.Id, ProgressEventType.SubmissionAdded, submittedBy,
            new { attemptNo = next, exerciseTitle = exercise.Title }, lesson.StageId, lesson.Id, exercise.Id);

        await db.SaveChangesAsync();
        await CourseLogic.CascadeAsync(db, lesson, course.Id);
        await db.SaveChangesAsync();
        return submission;
    }

    /// <summary>
    /// Mark one attempt. A regrade does not overwrite: it writes a new row pointing at the one it
    /// replaces, so a change of mind stays legible and the lesson score can be recomputed from
    /// whichever grade the course's scoring rule prefers.
    /// </summary>
    public static async Task<Grade> GradeAsync(RoadmapDbContext db, Course course, Submission submission,
        double score, double? maxScore, string? feedbackMd, object? rubric, string gradedBy, string? idempotencyKey)
    {
        if (idempotencyKey is not null)
        {
            var seen = await db.Grades
                .FirstOrDefaultAsync(g => g.SubmissionId == submission.Id && g.IdempotencyKey == idempotencyKey);
            if (seen is not null) return seen;
        }

        var exercise = await db.Exercises.FirstOrDefaultAsync(x => x.Id == submission.ExerciseId)
            ?? throw CourseException.NotFound("exercise");
        var max = maxScore ?? exercise.MaxScore;
        if (max <= 0) throw CourseException.Validation("maxScore must be greater than zero.");
        if (score < 0 || score > max)
            throw CourseException.Validation($"score must be between 0 and {max}.");
        CourseLogic.Md(feedbackMd, "feedbackMd");

        var prior = await db.Grades.Where(g => g.SubmissionId == submission.Id)
            .OrderByDescending(g => g.GradedAt).FirstOrDefaultAsync();

        var grade = new Grade
        {
            Id = Guid.NewGuid(), SubmissionId = submission.Id, Score = score, MaxScore = max,
            FeedbackMd = feedbackMd, GradedBy = gradedBy, IdempotencyKey = idempotencyKey,
            Rubric = rubric is null ? null : rubric as string ?? JsonSerializer.Serialize(rubric, CourseLogic.Json),
            SupersedesGradeId = prior?.Id,
        };
        db.Grades.Add(grade);

        var lesson = await db.Lessons.FirstOrDefaultAsync(l => l.Id == exercise.LessonId);
        CourseLogic.Event(db, course.Id, ProgressEventType.GradeAdded, gradedBy,
            new { score, maxScore = max, exerciseTitle = exercise.Title, regrade = prior is not null },
            lesson?.StageId, exercise.LessonId, exercise.Id);

        await db.SaveChangesAsync();
        return grade;
    }

    /// <summary>
    /// Where the course stands, in one call: what is open, what is waiting to be graded, what the
    /// last session said it was in the middle of, and — when nothing is open — the next stage that
    /// still has to be written.
    /// </summary>
    public static async Task<object> ResumeAsync(RoadmapDbContext db, Course course)
    {
        var tree = await CourseProgress.ComputeAsync(db, course.Id);

        var pending = tree.CurrentLesson?.Exercises
            .Where(x => !x.HasGrade)
            .Select(x => new { id = x.Id, kind = x.Kind, title = x.Title, hasSubmission = x.HasSubmission, hasGrade = x.HasGrade })
            .ToList();

        var handoff = await db.ProgressEvents.AsNoTracking()
            .Where(e => e.CourseId == course.Id && e.Type == ProgressEventType.Handoff)
            .OrderByDescending(e => e.Id).FirstOrDefaultAsync();

        var recentGrades = await db.Grades.AsNoTracking()
            .Where(g => g.Submission.Exercise.Lesson.Stage.CourseId == course.Id)
            .OrderByDescending(g => g.GradedAt).Take(10)
            .Select(g => new
            {
                lessonCode = g.Submission.Exercise.Lesson.Code,
                exerciseTitle = g.Submission.Exercise.Title,
                score = g.Score, maxScore = g.MaxScore, gradedAt = g.GradedAt,
            }).ToListAsync();

        var recentEvents = await TimelineAsync(db, course.Id, null, 20, null);

        return new
        {
            course = new
            {
                slug = course.Slug, title = course.Title, status = course.Status.Wire(),
                progress = tree.Progress, definedFraction = tree.DefinedFraction,
                hoursLogged = tree.HoursLogged, estimatedHoursRemaining = tree.EstimatedHoursRemaining,
            },
            currentStage = tree.CurrentStage is null ? null : new
            {
                code = tree.CurrentStage.Code, title = tree.CurrentStage.Title,
                status = tree.CurrentStage.Status.Wire(), progress = tree.CurrentStage.Progress,
            },
            currentLesson = tree.CurrentLesson is null ? null : new
            {
                code = tree.CurrentLesson.Code, title = tree.CurrentLesson.Title,
                status = tree.CurrentLesson.Status.Wire(), progress = tree.CurrentLesson.Progress,
                score = tree.CurrentLesson.Score,
            },
            pendingExercises = pending ?? [],
            latestHandoff = handoff is null ? null : new
            {
                md = HandoffText(handoff.Payload), createdAt = handoff.CreatedAt, actor = handoff.Actor,
            },
            recentGrades,
            nextUndefinedStage = tree.NextUndefinedStage is null ? null : new
            {
                code = tree.NextUndefinedStage.Code, title = tree.NextUndefinedStage.Title,
                placeholderLessons = tree.NextUndefinedStage.LessonsTotal - tree.NextUndefinedStage.LessonsDefined,
            },
            recentEvents,
        };
    }

    private static string? HandoffText(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("md", out var md) ? md.GetString() : null;
        }
        catch { return null; }
    }

    /// <summary>Newest first, cursor-paginated on the event id, which is monotonic.</summary>
    public static async Task<List<object>> TimelineAsync(RoadmapDbContext db, Guid courseId,
        long? cursor, int limit, string[]? types)
    {
        var q = db.ProgressEvents.AsNoTracking().Where(e => e.CourseId == courseId);
        if (cursor is long c) q = q.Where(e => e.Id < c);
        if (types is { Length: > 0 })
        {
            var parsed = types.Select(ParseType).Where(t => t is not null).Select(t => t!.Value).ToList();
            if (parsed.Count > 0) q = q.Where(e => parsed.Contains(e.Type));
        }

        var rows = await q.OrderByDescending(e => e.Id).Take(Math.Clamp(limit, 1, 200)).ToListAsync();
        return [.. rows.Select(e => (object)new
        {
            id = e.Id, type = Wire(e.Type), actor = e.Actor, createdAt = e.CreatedAt,
            stageId = e.StageId, lessonId = e.LessonId, exerciseId = e.ExerciseId,
            payload = JsonSerializer.Deserialize<JsonElement>(e.Payload),
        })];
    }

    /// <summary>The wire form of an event type is snake_case, as the spec writes it.</summary>
    public static string Wire(ProgressEventType t) => t switch
    {
        ProgressEventType.StatusChanged => "status_changed",
        ProgressEventType.SubmissionAdded => "submission_added",
        ProgressEventType.GradeAdded => "grade_added",
        ProgressEventType.TimeLogged => "time_logged",
        ProgressEventType.StructureChanged => "structure_changed",
        ProgressEventType.Handoff => "handoff",
        _ => "note",
    };

    public static ProgressEventType? ParseType(string? s) => s?.Replace("_", "").ToLowerInvariant() switch
    {
        "statuschanged" => ProgressEventType.StatusChanged,
        "submissionadded" => ProgressEventType.SubmissionAdded,
        "gradeadded" => ProgressEventType.GradeAdded,
        "note" => ProgressEventType.Note,
        "handoff" => ProgressEventType.Handoff,
        "timelogged" => ProgressEventType.TimeLogged,
        "structurechanged" => ProgressEventType.StructureChanged,
        _ => null,
    };
}
