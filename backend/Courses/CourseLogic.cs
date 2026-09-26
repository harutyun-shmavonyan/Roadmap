using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Courses;

/// <summary>Thrown by the logic below; the REST and MCP layers turn it into their own shapes.</summary>
public sealed class CourseException(string kind, object payload) : Exception(kind)
{
    public string Kind { get; } = kind;
    public object Payload { get; } = payload;

    public static CourseException Validation(params string[] details) =>
        new("validation", new { error = "validation", details });

    public static CourseException NotFound(string what) =>
        new("not_found", new { error = "not_found", detail = what });

    public static CourseException Transition(string from, string to, IEnumerable<string> allowed) =>
        new("invalid_transition", new { error = "invalid_transition", from, to, allowed = allowed.ToArray() });
}

/// <summary>A section kind a lesson of this course has, in template order.</summary>
public sealed record TemplateSection(string Kind, string? Title = null, bool Required = true, bool HasExercises = false);

/// <summary>A kind of gradeable work, and the defaults a new exercise of that kind inherits.</summary>
public sealed record TemplateExerciseKind(string Kind, string? Label = null, double DefaultMaxScore = 10,
    double DefaultWeight = 1, string? SectionKind = null);

/// <summary>
/// Everything the Courses feature knows how to do, in one place, because every mutation has to
/// leave a <see cref="ProgressEvent"/> behind and there is no way to guarantee that if callers
/// write rows themselves.
///
/// Static like <c>NewsletterLogic</c> and <c>ArticleLogic</c> rather than an injected service —
/// same reason: it is a pure function of the context it is handed.
/// </summary>
public static class CourseLogic
{
    /// <summary>Agents write camelCase; so does everything we store back.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>A Markdown field this big is a mistake, not a lesson.</summary>
    public const int MaxMarkdownBytes = 200 * 1024;
    public const int MaxLinks = 50;

    // ===== Parsing the template =====

    public static List<TemplateSection> Sections(LessonTemplate t) =>
        JsonSerializer.Deserialize<List<TemplateSection>>(t.Sections, Json) ?? [];

    public static List<TemplateExerciseKind> ExerciseKinds(LessonTemplate t) =>
        JsonSerializer.Deserialize<List<TemplateExerciseKind>>(t.ExerciseKinds, Json) ?? [];

    // ===== Validation helpers =====

    public static string? Md(string? value, string field)
    {
        if (value is null) return null;
        if (System.Text.Encoding.UTF8.GetByteCount(value) > MaxMarkdownBytes)
            throw CourseException.Validation($"{field} exceeds {MaxMarkdownBytes / 1024} KB.");
        return value;
    }

    public static string LinksJson(object? links)
    {
        if (links is null) return "[]";
        var json = links as string ?? JsonSerializer.Serialize(links, Json);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw CourseException.Validation("links must be an array of {label, url, kind}.");
        if (doc.RootElement.GetArrayLength() > MaxLinks)
            throw CourseException.Validation($"links has more than {MaxLinks} entries.");
        return json;
    }

    /// <summary>Lower-case, dash-separated, and unique — generated when a caller omits one.</summary>
    public static string Slugify(string text)
    {
        var chars = text.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-') is { Length: > 0 } s ? s[..Math.Min(s.Length, 128)] : "course";
    }

    // ===== Lookups =====

    public static async Task<Course> CourseByRefAsync(RoadmapDbContext db, Guid? id, string? slug, bool track = false)
    {
        var q = track ? db.Courses : db.Courses.AsNoTracking();
        var course = id is Guid cid
            ? await q.FirstOrDefaultAsync(c => c.Id == cid)
            : slug is not null ? await q.FirstOrDefaultAsync(c => c.Slug == slug) : null;
        return course ?? throw CourseException.NotFound($"course {(object?)id ?? slug}");
    }

    public static async Task<LessonTemplate> TemplateAsync(RoadmapDbContext db, Guid courseTemplateId) =>
        await db.LessonTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == courseTemplateId)
        ?? throw CourseException.NotFound("template");

    public static async Task<Stage> StageByCodeAsync(RoadmapDbContext db, Guid courseId, string code, bool track = false)
    {
        var q = track ? db.Stages : db.Stages.AsNoTracking();
        return await q.FirstOrDefaultAsync(s => s.CourseId == courseId && s.Code == code)
            ?? throw CourseException.NotFound($"stage {code}");
    }

    /// <summary>Lesson codes are unique per course, so this joins through the stage.</summary>
    public static async Task<Lesson> LessonByCodeAsync(RoadmapDbContext db, Guid courseId, string code, bool track = false)
    {
        var q = track ? db.Lessons : db.Lessons.AsNoTracking();
        return await q.FirstOrDefaultAsync(l => l.Code == code && l.Stage.CourseId == courseId)
            ?? throw CourseException.NotFound($"lesson {code}");
    }

    public static async Task<Guid> CourseIdOfLessonAsync(RoadmapDbContext db, Guid lessonId) =>
        await db.Lessons.AsNoTracking().Where(l => l.Id == lessonId)
            .Select(l => l.Stage.CourseId).FirstOrDefaultAsync();

    // ===== Events =====

    /// <summary>
    /// Record what just happened. Every mutation calls this; nothing else writes the table. The
    /// caller still has to SaveChanges — the event rides along in the same transaction as the
    /// change it describes, which is the point.
    /// </summary>
    public static ProgressEvent Event(RoadmapDbContext db, Guid courseId, ProgressEventType type, string actor,
        object? payload = null, Guid? stageId = null, Guid? lessonId = null, Guid? exerciseId = null)
    {
        var ev = new ProgressEvent
        {
            CourseId = courseId, Type = type, Actor = actor,
            StageId = stageId, LessonId = lessonId, ExerciseId = exerciseId,
            Payload = payload is null ? "{}" : JsonSerializer.Serialize(payload, Json),
        };
        db.ProgressEvents.Add(ev);
        return ev;
    }

    // ===== Status machines =====

    private static readonly Dictionary<CourseStatus, CourseStatus[]> CourseMoves = new()
    {
        [CourseStatus.Draft] = [CourseStatus.Active, CourseStatus.Archived],
        [CourseStatus.Active] = [CourseStatus.Paused, CourseStatus.Completed, CourseStatus.Archived],
        [CourseStatus.Paused] = [CourseStatus.Active, CourseStatus.Archived],
        [CourseStatus.Completed] = [CourseStatus.Active, CourseStatus.Archived],
        [CourseStatus.Archived] = [],
    };

    private static readonly Dictionary<StageStatus, StageStatus[]> StageMoves = new()
    {
        [StageStatus.Planned] = [StageStatus.Defined, StageStatus.InProgress, StageStatus.Skipped],
        [StageStatus.Defined] = [StageStatus.Planned, StageStatus.InProgress, StageStatus.Skipped],
        [StageStatus.InProgress] = [StageStatus.Defined, StageStatus.Completed, StageStatus.Skipped],
        [StageStatus.Completed] = [StageStatus.InProgress, StageStatus.Skipped],
        [StageStatus.Skipped] = [StageStatus.Planned, StageStatus.Defined, StageStatus.InProgress],
    };

    private static readonly Dictionary<LessonStatus, LessonStatus[]> LessonMoves = new()
    {
        [LessonStatus.Placeholder] = [LessonStatus.Draft, LessonStatus.Ready, LessonStatus.Skipped],
        [LessonStatus.Draft] = [LessonStatus.Placeholder, LessonStatus.Ready, LessonStatus.Skipped],
        [LessonStatus.Ready] = [LessonStatus.Draft, LessonStatus.InProgress, LessonStatus.Skipped],
        [LessonStatus.InProgress] = [LessonStatus.Ready, LessonStatus.Submitted, LessonStatus.Completed, LessonStatus.Skipped],
        [LessonStatus.Submitted] = [LessonStatus.InProgress, LessonStatus.Completed, LessonStatus.Skipped],
        [LessonStatus.Completed] = [LessonStatus.InProgress, LessonStatus.Skipped],
        [LessonStatus.Skipped] = [LessonStatus.Placeholder, LessonStatus.Draft, LessonStatus.Ready],
    };

    public static void CheckCourseMove(CourseStatus from, CourseStatus to)
    {
        if (from == to) return;
        if (!CourseMoves[from].Contains(to))
            throw CourseException.Transition(from.ToString(), to.ToString(), CourseMoves[from].Select(x => x.ToString()));
    }

    public static void CheckStageMove(StageStatus from, StageStatus to)
    {
        if (from == to) return;
        if (!StageMoves[from].Contains(to))
            throw CourseException.Transition(from.ToString(), to.ToString(), StageMoves[from].Select(x => x.ToString()));
    }

    public static void CheckLessonMove(LessonStatus from, LessonStatus to)
    {
        if (from == to) return;
        if (!LessonMoves[from].Contains(to))
            throw CourseException.Transition(from.ToString(), to.ToString(), LessonMoves[from].Select(x => x.ToString()));
    }

    /// <summary>
    /// Ready means "a reader could sit down with this", so it is checked against the template's
    /// required section kinds rather than taken on trust. Completed means the work was marked, so
    /// it needs a grade on every required exercise — <paramref name="force"/> overrides that and
    /// the override lands in the event.
    /// </summary>
    public static async Task GuardLessonTargetAsync(RoadmapDbContext db, Lesson lesson, LessonTemplate template,
        LessonStatus to, bool force)
    {
        if (to == LessonStatus.Ready)
        {
            var have = await db.LessonSections.AsNoTracking()
                .Where(s => s.LessonId == lesson.Id).Select(s => s.Kind).ToListAsync();
            var missing = Sections(template).Where(s => s.Required && !have.Contains(s.Kind))
                .Select(s => s.Kind).ToList();
            if (missing.Count > 0)
                throw new CourseException("template", new
                {
                    error = "template_violation",
                    detail = "Lesson is missing sections the template requires.",
                    missingSectionKinds = missing,
                });
        }

        if (to == LessonStatus.Completed && !force)
        {
            var ungraded = await db.Exercises.AsNoTracking()
                .Where(x => x.LessonId == lesson.Id && x.Required)
                .Where(x => !x.Submissions.Any(s => s.Grades.Any()))
                .Select(x => x.Title).ToListAsync();
            if (ungraded.Count > 0)
                throw CourseException.Validation(
                    ["Required exercises have no grade yet; pass force to complete anyway.", .. ungraded]);
        }
    }

    // ===== Auto-transitions =====

    /// <summary>
    /// Pull the lesson and its stage along behind the work. Submitting the first thing on a ready
    /// lesson starts it; submitting the last required thing marks it submitted; a stage follows
    /// its lessons. All of it is recorded with <c>system</c> as the actor so the timeline
    /// distinguishes what was decided from what merely followed.
    /// </summary>
    public static async Task CascadeAsync(RoadmapDbContext db, Lesson lesson, Guid courseId)
    {
        var required = await db.Exercises.AsNoTracking()
            .Where(x => x.LessonId == lesson.Id && x.Required)
            .Select(x => new { HasSubmission = x.Submissions.Any(), HasGrade = x.Submissions.Any(s => s.Grades.Any()) })
            .ToListAsync();

        var target = lesson.Status;
        if (lesson.Status is LessonStatus.Ready or LessonStatus.Draft && required.Any(r => r.HasSubmission))
            target = LessonStatus.InProgress;
        if (lesson.Status is LessonStatus.InProgress or LessonStatus.Ready
            && required.Count > 0 && required.All(r => r.HasSubmission))
            target = LessonStatus.Submitted;

        if (target != lesson.Status)
        {
            var from = lesson.Status;
            lesson.Status = target;
            lesson.UpdatedAt = DateTime.UtcNow;
            Event(db, courseId, ProgressEventType.StatusChanged, "system",
                new { from = from.ToString(), to = target.ToString() }, lesson.StageId, lesson.Id);
        }

        var stage = await db.Stages.FirstOrDefaultAsync(s => s.Id == lesson.StageId);
        if (stage is null) return;

        var siblings = await db.Lessons.AsNoTracking().Where(l => l.StageId == stage.Id)
            .Select(l => l.Status).ToListAsync();
        var live = siblings.Where(st => st != LessonStatus.Skipped).ToList();

        var stageTarget = stage.Status;
        if (stage.Status is StageStatus.Planned or StageStatus.Defined
            && siblings.Any(st => st is LessonStatus.InProgress or LessonStatus.Submitted or LessonStatus.Completed))
            stageTarget = StageStatus.InProgress;
        if (live.Count > 0 && live.All(st => st == LessonStatus.Completed))
            stageTarget = StageStatus.Completed;

        if (stageTarget != stage.Status)
        {
            var from = stage.Status;
            stage.Status = stageTarget;
            stage.UpdatedAt = DateTime.UtcNow;
            Event(db, courseId, ProgressEventType.StatusChanged, "system",
                new { from = from.ToString(), to = stageTarget.ToString() }, stage.Id);
        }
    }
}
