using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Courses;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

/// <summary>
/// The Courses tab's agent-facing side, and the primary way courses are written — the REST API
/// only reads. The shape of a session is: <c>get_course_resume</c> to find out where things
/// stand, <c>get_lesson</c> to reload what you were in the middle of, the structure tools to
/// write the next lesson, <c>add_submission</c> and <c>grade_submission</c> to record work, then
/// <c>log_course_event</c> with a handoff so the next session knows where you stopped.
///
/// Every tool that creates something keyed on a natural key — a course <c>slug</c>, a stage or
/// lesson <c>code</c>, a section <c>kind</c> — returns what is already there with
/// <c>created: false</c> instead of failing, so a retry is never an error.
/// </summary>
[McpServerToolType]
public sealed class CourseMcpTools(RoadmapDbContext db)
{
    // camelCase, so a record or an inferred anonymous member name (new { stage.Code }) reads the
    // same on the wire as the hand-written ones. Nulls are kept: "score": null says ungraded,
    // where an absent key would only say we did not look.
    private static readonly JsonSerializerOptions Out = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        WriteIndented = true,
    };

    private static string J(object? v) => JsonSerializer.Serialize(v, Out);

    /// <summary>Turn the logic layer's exceptions into the error shapes the spec promises.</summary>
    private static async Task<string> Run(Func<Task<object>> body)
    {
        try { return J(await body()); }
        catch (CourseException ex) { return J(ex.Payload); }
        catch (DbUpdateException ex)
        {
            return J(new { error = "conflict", detail = ex.InnerException?.Message ?? ex.Message });
        }
    }

    private async Task<Course> CourseRef(Guid? id, string? slug, bool track = false) =>
        await CourseLogic.CourseByRefAsync(db, id, slug, track);

    // The spec names a lesson `code` on the lesson tools and `lessonCode` on the tools that hang
    // off one. An agent writing a whole stage moves between the two constantly, so both are taken
    // everywhere rather than failing on the wrong one of two right answers.
    private static string LessonRef(string? code, string? lessonCode) =>
        code ?? lessonCode ?? throw CourseException.Validation("code (or lessonCode) is required.");

    private static string StageRefCode(string? code, string? stageCode) =>
        code ?? stageCode ?? throw CourseException.Validation("code (or stageCode) is required.");

    private async Task<object> ProgressEcho(Guid courseId, string? lessonCode = null)
    {
        var tree = await CourseProgress.ComputeAsync(db, courseId);
        var lesson = lessonCode is null ? null
            : tree.Stages.SelectMany(s => s.Lessons).FirstOrDefault(l => l.Code == lessonCode);
        var stage = lesson is null ? null : tree.Stages.FirstOrDefault(s => s.Lessons.Contains(lesson));
        return new
        {
            course = new { progress = tree.Progress, definedFraction = tree.DefinedFraction },
            stage = stage is null ? null : new { stage.Code, stage.Progress, stage.DefinedFraction },
            lesson = lesson is null ? null : new { lesson.Code, lesson.Status, lesson.Progress, lesson.Score },
        };
    }

    // ==================== 6.1 Course and template ====================

    [McpServerTool(Name = "list_lesson_templates"), Description(
        "List the lesson templates available. A template is the rhythm a course's lessons follow: the ordered " +
        "section kinds every lesson has, and the kinds of exercise that can hang off them. Courses reference a " +
        "template by name, and a template is reusable across courses.")]
    public async Task<string> ListLessonTemplates() => await Run(async () =>
    {
        var rows = await db.LessonTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        return rows.Select(t => new
        {
            id = t.Id, name = t.Name, description = t.Description,
            sections = JsonSerializer.Deserialize<JsonElement>(t.Sections),
            exerciseKinds = JsonSerializer.Deserialize<JsonElement>(t.ExerciseKinds),
            usedByCourses = db.Courses.Count(c => c.TemplateId == t.Id),
        }).ToList();
    });

    [McpServerTool(Name = "create_lesson_template"), Description(
        "Create a lesson template. IDEMPOTENT ON NAME: if one already exists with this name it is returned " +
        "unchanged with created=false — use update_lesson_template to change it.\n\n" +
        "sections is an ordered array of {kind, title, required, hasExercises}; kind is a free slug (the UI maps " +
        "known ones to icons and falls back to a generic one). A lesson can only reach 'ready' once every " +
        "required kind is present.\n" +
        "exerciseKinds is [{kind, label, defaultMaxScore, defaultWeight, sectionKind}] — the gradeable work this " +
        "course knows about, and what a new exercise of that kind inherits when the caller omits a score or weight.")]
    public async Task<string> CreateLessonTemplate(
        [Description("Unique name, e.g. 'problem-first-7-step'")] string name,
        [Description("Ordered array of {kind, title, required, hasExercises}")] JsonElement sections,
        [Description("Array of {kind, label, defaultMaxScore, defaultWeight, sectionKind}")] JsonElement exerciseKinds,
        [Description("What this rhythm is for")] string? description = null) => await Run(async () =>
    {
        var existing = await db.LessonTemplates.FirstOrDefaultAsync(t => t.Name == name);
        if (existing is not null)
            return new { created = false, id = existing.Id, name = existing.Name };

        var t = new LessonTemplate
        {
            Id = Guid.NewGuid(), Name = name, Description = description,
            Sections = sections.ValueKind == JsonValueKind.Array ? sections.GetRawText() : "[]",
            ExerciseKinds = exerciseKinds.ValueKind == JsonValueKind.Array ? exerciseKinds.GetRawText() : "[]",
        };
        db.LessonTemplates.Add(t);
        await db.SaveChangesAsync();
        return new { created = true, id = t.Id, name = t.Name };
    });

    [McpServerTool(Name = "update_lesson_template"), Description(
        "Change a template's sections, exercise kinds or description. REFUSES to remove a section kind that a " +
        "lesson of a non-archived course is currently using, since that would orphan written content — delete " +
        "those sections first. Omitted arguments are left unchanged.")]
    public async Task<string> UpdateLessonTemplate(
        [Description("Template id")] Guid? id = null,
        [Description("Template name, if you do not have the id")] string? name = null,
        [Description("Replacement ordered array of {kind, title, required, hasExercises}")] JsonElement? sections = null,
        [Description("Replacement array of exercise kinds")] JsonElement? exerciseKinds = null,
        [Description("New description")] string? description = null) => await Run(async () =>
    {
        var t = id is Guid tid
            ? await db.LessonTemplates.FirstOrDefaultAsync(x => x.Id == tid)
            : name is not null ? await db.LessonTemplates.FirstOrDefaultAsync(x => x.Name == name) : null;
        if (t is null) throw CourseException.NotFound("template");

        if (sections is JsonElement secs && secs.ValueKind == JsonValueKind.Array)
        {
            var keeping = JsonSerializer.Deserialize<List<TemplateSection>>(secs.GetRawText(), CourseLogic.Json)
                ?? [];
            var kinds = keeping.Select(k => k.Kind).ToHashSet();
            var inUse = await db.LessonSections.AsNoTracking()
                .Where(s => s.Lesson.Stage.Course.TemplateId == t.Id
                    && s.Lesson.Stage.Course.Status != CourseStatus.Archived)
                .Select(s => s.Kind).Distinct().ToListAsync();
            var orphaned = inUse.Where(k => !kinds.Contains(k)).ToList();
            if (orphaned.Count > 0)
                throw CourseException.Validation(
                    [.. orphaned.Select(k => $"section kind '{k}' is in use by a lesson and cannot be removed")]);
            t.Sections = secs.GetRawText();
        }
        if (exerciseKinds is JsonElement ek && ek.ValueKind == JsonValueKind.Array) t.ExerciseKinds = ek.GetRawText();
        if (description is not null) t.Description = description;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new { id = t.Id, name = t.Name, updated = true };
    });

    [McpServerTool(Name = "list_courses"), Description(
        "List courses newest-first with their computed progress, how much of them is defined yet, hours logged " +
        "and what is currently in focus. Optionally filter by status (draft|active|paused|completed|archived).")]
    public async Task<string> ListCourses(
        [Description("Only courses in this status")] string? status = null) => await Run(async () =>
        await CourseViews.SummariesAsync(db,
            status is not null && Enum.TryParse<CourseStatus>(status.Replace("_", ""), true, out var st)
                ? st : null));

    [McpServerTool(Name = "get_course"), Description(
        "Get a course as a tree. depth='stages' returns stages only; 'lessons' adds each stage's lessons with " +
        "status, progress and score; 'full' also includes every section's Markdown and every exercise's prompt " +
        "(but no submissions — use get_lesson for those). Address the course by id or slug.")]
    public async Task<string> GetCourse(
        [Description("Course id")] Guid? id = null,
        [Description("Course slug")] string? slug = null,
        [Description("'stages' | 'lessons' | 'full' (default 'lessons')")] string depth = "lessons")
        => await Run(async () => await CourseViews.DetailAsync(db, await CourseRef(id, slug), depth));

    [McpServerTool(Name = "create_course"), Description(
        "Create a course. IDEMPOTENT ON SLUG: an existing course with this slug is returned with created=false " +
        "rather than being overwritten. The template must already exist (list_lesson_templates / " +
        "create_lesson_template), or use import_course_outline which can create both in one call. " +
        "A new course starts in 'draft'; set_course_status moves it to 'active'.")]
    public async Task<string> CreateCourse(
        [Description("Unique slug, e.g. 'langchain-mastery'")] string slug,
        [Description("Course title")] string title,
        [Description("Template name")] string? templateName = null,
        [Description("Template id, if you have it instead of the name")] Guid? templateId = null,
        [Description("One-line subtitle")] string? subtitle = null,
        [Description("What the course is, in Markdown")] string? descriptionMd = null,
        [Description("The long-running piece of work it builds towards, in Markdown")] string? capstoneMd = null,
        [Description("The course's instructions.md — how it is to be taught, graded and written. Handed back by get_course_resume every session.")]
        string? instructionsMd = null,
        [Description("Study budget in hours per week")] double? targetHoursPerWeek = null,
        [Description("Free JSON. 'scoring' ('best'|'latest', default 'best') decides which grade counts.")]
        JsonElement? metadata = null) => await Run(async () =>
    {
        var existing = await db.Courses.FirstOrDefaultAsync(c => c.Slug == slug);
        if (existing is not null)
            return new { created = false, id = existing.Id, slug = existing.Slug, status = existing.Status.Wire() };

        var template = templateId is Guid tid
            ? await db.LessonTemplates.FirstOrDefaultAsync(t => t.Id == tid)
            : templateName is not null ? await db.LessonTemplates.FirstOrDefaultAsync(t => t.Name == templateName) : null;
        if (template is null) throw CourseException.NotFound("template (pass templateName or templateId)");

        var course = new Course
        {
            Id = Guid.NewGuid(), Slug = slug, Title = title, Subtitle = subtitle,
            DescriptionMd = CourseLogic.Md(descriptionMd, "descriptionMd"),
            CapstoneMd = CourseLogic.Md(capstoneMd, "capstoneMd"),
            InstructionsMd = CourseLogic.Md(instructionsMd, "instructionsMd"),
            TemplateId = template.Id, TargetHoursPerWeek = targetHoursPerWeek,
            Metadata = metadata is JsonElement m && m.ValueKind == JsonValueKind.Object ? m.GetRawText() : "{}",
        };
        db.Courses.Add(course);
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, "agent:mcp",
            new { summary = $"course {slug} created" });
        await db.SaveChangesAsync();
        return new { created = true, id = course.Id, slug = course.Slug, status = course.Status.Wire() };
    });

    [McpServerTool(Name = "update_course"), Description(
        "Change a course's title, subtitle, description, capstone, instructions, weekly budget or metadata. " +
        "Omitted arguments are left unchanged. Use set_course_status for status, and set_course_instructions " +
        "if instructions.md is the only thing you are writing.")]
    public async Task<string> UpdateCourse(
        [Description("Course id")] Guid? id = null,
        [Description("Course slug")] string? slug = null,
        [Description("New title")] string? title = null,
        [Description("New subtitle")] string? subtitle = null,
        [Description("New description Markdown")] string? descriptionMd = null,
        [Description("New capstone Markdown")] string? capstoneMd = null,
        [Description("Replacement instructions.md, in Markdown")] string? instructionsMd = null,
        [Description("New weekly hours budget")] double? targetHoursPerWeek = null,
        [Description("Replacement metadata object")] JsonElement? metadata = null) => await Run(async () =>
    {
        var course = await CourseRef(id, slug, track: true);
        if (title is not null) course.Title = title;
        if (subtitle is not null) course.Subtitle = subtitle;
        if (descriptionMd is not null) course.DescriptionMd = CourseLogic.Md(descriptionMd, "descriptionMd");
        if (capstoneMd is not null) course.CapstoneMd = CourseLogic.Md(capstoneMd, "capstoneMd");
        if (instructionsMd is not null) course.InstructionsMd = CourseLogic.Md(instructionsMd, "instructionsMd");
        if (targetHoursPerWeek is not null) course.TargetHoursPerWeek = targetHoursPerWeek;
        if (metadata is JsonElement m && m.ValueKind == JsonValueKind.Object) course.Metadata = m.GetRawText();
        course.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new { id = course.Id, slug = course.Slug, updated = true };
    });

    [McpServerTool(Name = "get_course_instructions"), Description(
        "Read a course's instructions.md — the house rules for how this course is taught, graded and written. " +
        "get_course_resume already returns it, so this is for when you want it on its own (to review it, or to " +
        "edit it and write it back). Returns { slug, instructionsMd, bytes, updatedAt }; instructionsMd is null " +
        "when the course has none.")]
    public async Task<string> GetCourseInstructions(
        [Description("Course slug")] string? slug = null,
        [Description("Course id")] Guid? id = null) => await Run(async () =>
    {
        var course = await CourseRef(id, slug);
        return new
        {
            slug = course.Slug, instructionsMd = course.InstructionsMd,
            bytes = course.InstructionsMd is null ? 0 : Encoding.UTF8.GetByteCount(course.InstructionsMd),
            updatedAt = course.UpdatedAt,
        };
    });

    [McpServerTool(Name = "set_course_instructions"), Description(
        "Write a course's instructions.md. REPLACES the whole document — read it first if you mean to amend it " +
        "rather than start it over; pass append=true to add to the end instead. This is the file a session reads " +
        "before it touches the course: how to write a lesson for it, how to mark the work, what the course's " +
        "conventions are. Pass an empty string to clear it. Capped at 200 KB like every Markdown field.")]
    public async Task<string> SetCourseInstructions(
        [Description("The instructions.md content, in Markdown")] string instructionsMd,
        [Description("Course slug")] string? slug = null,
        [Description("Course id")] Guid? id = null,
        [Description("Add to the end of the existing document instead of replacing it")] bool append = false)
        => await Run(async () =>
    {
        var course = await CourseRef(id, slug, track: true);
        var next = append && !string.IsNullOrEmpty(course.InstructionsMd)
            ? course.InstructionsMd!.TrimEnd() + "\n\n" + instructionsMd.TrimStart()
            : instructionsMd;
        var had = course.InstructionsMd;
        course.InstructionsMd = string.IsNullOrWhiteSpace(next) ? null : CourseLogic.Md(next, "instructionsMd");
        course.UpdatedAt = DateTime.UtcNow;
        // Instructions change how everything after them is written, so the change is on the record
        // rather than silent — a lesson graded under the old rules should be explainable later.
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, "agent:mcp", new
        {
            summary = course.InstructionsMd is null ? "instructions.md cleared"
                : had is null ? "instructions.md written"
                : append ? "instructions.md appended to" : "instructions.md replaced",
            bytes = course.InstructionsMd is null ? 0 : Encoding.UTF8.GetByteCount(course.InstructionsMd),
        });
        await db.SaveChangesAsync();
        return new
        {
            slug = course.Slug, saved = true,
            bytes = course.InstructionsMd is null ? 0 : Encoding.UTF8.GetByteCount(course.InstructionsMd),
        };
    });

    [McpServerTool(Name = "set_course_status"), Description(
        "Move a course through draft -> active -> (paused <-> active) -> completed -> archived. 'archived' is " +
        "terminal. An invalid move returns {error:'invalid_transition', from, to, allowed}. Going active for the " +
        "first time stamps startedAt; completing stamps completedAt.")]
    public async Task<string> SetCourseStatus(
        [Description("draft | active | paused | completed | archived")] string status,
        [Description("Course id")] Guid? id = null,
        [Description("Course slug")] string? slug = null) => await Run(async () =>
    {
        var course = await CourseRef(id, slug, track: true);
        if (!Enum.TryParse<CourseStatus>(status, true, out var to))
            throw CourseException.Validation($"unknown status '{status}'.");
        CourseStructure.SetCourseStatus(db, course, to, "agent:mcp");
        await db.SaveChangesAsync();
        return new { id = course.Id, slug = course.Slug, status = course.Status.Wire() };
    });

    [McpServerTool(Name = "archive_course"), Description(
        "Archive a course. Terminal — it stays readable but can no longer change status. Shorthand for " +
        "set_course_status(status='archived').")]
    public async Task<string> ArchiveCourse(
        [Description("Course id")] Guid? id = null,
        [Description("Course slug")] string? slug = null) => await SetCourseStatus("archived", id, slug);

    // ==================== 6.2 Structure ====================

    [McpServerTool(Name = "import_course_outline"), Description(
        "Create or update a WHOLE COURSE from one JSON document — the way a plan becomes a course. This is the " +
        "tool to reach for first.\n\n" +
        "Shape: { course: {slug, title, subtitle?, descriptionMd?, capstoneMd?, templateName, targetHoursPerWeek?, " +
        "metadata?}, template?: {name, sections[], exerciseKinds[]}, stages: [{code, title, targetWeeks?, " +
        "summaryMd?, status?, lessons: [{code, title, estimatedHours?, status?, sections: [{kind, contentMd}], " +
        "exercises: [{kind, title, promptMd, referenceMd?}]}]}], resources?: [{title, url, kind?, noteMd?, lessonCode?}] }\n\n" +
        "UPSERTS, NEVER DELETES. Keyed on course slug, stage code, lesson code, section kind and exercise " +
        "(kind + title). A field the document omits is left as it is, so a later, fuller document fills a course " +
        "in rather than replacing it. Running the same document twice reports everything as unchanged. Lesson " +
        "status may only move forward out of placeholder/draft, and never over a lesson that already has " +
        "submissions. Partial definition is expected: a stage may have no lessons and a lesson may be nothing " +
        "but a code and a title. Returns created/updated/unchanged counts per level plus any per-item errors.")]
    public async Task<string> ImportCourseOutline(
        [Description("The outline document (see the shape above)")] JsonElement outline)
        => await Run(async () => await CourseOutline.ImportAsync(db, outline, "agent:mcp"));

    [McpServerTool(Name = "create_stage"), Description(
        "Add a stage (milestone) to a course. IDEMPOTENT ON CODE within the course. position defaults to the end; " +
        "giving one inserts there and shifts the rest down. targetWeeks doubles as the stage's weight in course " +
        "progress — stages without one count as 1.")]
    public async Task<string> CreateStage(
        [Description("Stage title")] string title,
        [Description("Short code unique in the course, e.g. 'M0.5'")] string? code = null,
        [Description("Course slug")] string? courseSlug = null,
        [Description("Course id")] Guid? courseId = null,
        [Description("Markdown summary")] string? summaryMd = null,
        [Description("Expected weeks; also its weight in course progress")] double? targetWeeks = null,
        [Description("0-based position; defaults to the end")] int? position = null,
        [Description("Alias for code")] string? stageCode = null) => await Run(async () =>
    {
        code = StageRefCode(code, stageCode);
        var course = await CourseRef(courseId, courseSlug);
        var (stage, created) = await CourseStructure.CreateStageAsync(db, course, code, title, summaryMd,
            targetWeeks, position, "agent:mcp");
        await db.SaveChangesAsync();
        return new { created, id = stage.Id, code = stage.Code, status = stage.Status.Wire(),
            progress = await ProgressEcho(course.Id) };
    });

    [McpServerTool(Name = "update_stage"), Description(
        "Change a stage's title, summary or targetWeeks. Address it by id, or by course + code. Omitted " +
        "arguments are left unchanged. Use set_stage_status for status.")]
    public async Task<string> UpdateStage(
        [Description("Stage id")] Guid? id = null,
        [Description("Course slug (with code)")] string? courseSlug = null,
        [Description("Stage code (with courseSlug)")] string? code = null,
        [Description("New title")] string? title = null,
        [Description("New Markdown summary")] string? summaryMd = null,
        [Description("New expected weeks")] double? targetWeeks = null,
        [Description("Alias for code")] string? stageCode = null) => await Run(async () =>
    {
        var (course, stage) = await StageRef(id, courseSlug, code ?? stageCode, track: true);
        if (title is not null) stage.Title = title;
        if (summaryMd is not null) stage.SummaryMd = CourseLogic.Md(summaryMd, "summaryMd");
        if (targetWeeks is not null) stage.TargetWeeks = targetWeeks;
        stage.UpdatedAt = DateTime.UtcNow;
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, "agent:mcp",
            new { summary = $"stage {stage.Code} updated" }, stage.Id);
        await db.SaveChangesAsync();
        return new { id = stage.Id, code = stage.Code, updated = true, progress = await ProgressEcho(course.Id) };
    });

    [McpServerTool(Name = "set_stage_status"), Description(
        "Move a stage through planned -> defined -> in_progress -> completed; anything may be skipped, and a " +
        "completed stage may be reopened to in_progress. Stages also advance on their own as their lessons do, " +
        "so you rarely need this. Invalid moves return {error:'invalid_transition', from, to, allowed}.")]
    public async Task<string> SetStageStatus(
        [Description("planned | defined | in_progress | completed | skipped")] string status,
        [Description("Stage id")] Guid? id = null,
        [Description("Course slug (with code)")] string? courseSlug = null,
        [Description("Stage code (with courseSlug)")] string? code = null,
        [Description("Alias for code")] string? stageCode = null) => await Run(async () =>
    {
        var (course, stage) = await StageRef(id, courseSlug, code ?? stageCode, track: true);
        if (!Enum.TryParse<StageStatus>(status.Replace("_", ""), true, out var to))
            throw CourseException.Validation($"unknown status '{status}'.");
        CourseStructure.SetStageStatus(db, course, stage, to, "agent:mcp");
        await db.SaveChangesAsync();
        return new { id = stage.Id, code = stage.Code, status = stage.Status.Wire(),
            progress = await ProgressEcho(course.Id) };
    });

    [McpServerTool(Name = "reorder_stages"), Description(
        "Set the order of a course's stages. Pass the codes in the order you want; any stage you leave out keeps " +
        "its relative order behind the ones you named. Positions come out dense.")]
    public async Task<string> ReorderStages(
        [Description("Course slug")] string courseSlug,
        [Description("Stage codes, in the order wanted")] string[] codesInOrder) => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        await CourseStructure.ReorderStagesAsync(db, course, [.. codesInOrder], "agent:mcp");
        await db.SaveChangesAsync();
        var tree = await CourseProgress.ComputeAsync(db, course.Id);
        return new { order = tree.Stages.Select(s => s.Code) };
    });

    [McpServerTool(Name = "delete_stage"), Description(
        "Soft-delete a stage and its lessons. REFUSES if any lesson under it has submissions, unless cascade=true " +
        "— deleting work that was actually done should be deliberate.")]
    public async Task<string> DeleteStage(
        [Description("Stage id")] Guid? id = null,
        [Description("Course slug (with code)")] string? courseSlug = null,
        [Description("Stage code (with courseSlug)")] string? code = null,
        [Description("Delete even if lessons have submissions")] bool cascade = false,
        [Description("Alias for code")] string? stageCode = null) => await Run(async () =>
    {
        var (course, stage) = await StageRef(id, courseSlug, code ?? stageCode, track: true);
        await CourseStructure.DeleteStageAsync(db, course, stage, cascade, "agent:mcp");
        await db.SaveChangesAsync();
        return new { deleted = true, code = stage.Code, progress = await ProgressEcho(course.Id) };
    });

    [McpServerTool(Name = "create_lesson"), Description(
        "Add a lesson to a stage — and, if you pass sections and exercises, define it fully in this one call. " +
        "IDEMPOTENT ON CODE within the course (codes are course-wide, not stage-wide, so a lesson keeps its name " +
        "when it moves stages).\n\n" +
        "sections: [{kind, title?, contentMd}] — kinds must be in the course template.\n" +
        "exercises: [{kind, title, promptMd, referenceMd?, maxScore?, weight?, required?}] — kinds must be in the " +
        "template's exerciseKinds; score and weight default from it.\n\n" +
        "status defaults to 'placeholder', which is a valid, empty lesson. Pass 'ready' only once every section " +
        "the template requires is present, or it is refused with the list of missing kinds.")]
    public async Task<string> CreateLesson(
        [Description("Course slug")] string courseSlug,
        [Description("Lesson code, unique in the course, e.g. '0.5.2'")] string code,
        [Description("Lesson title")] string title,
        [Description("Stage code to put it under")] string? stageCode = null,
        [Description("Stage id, if you have it instead of the code")] Guid? stageId = null,
        [Description("Markdown summary")] string? summaryMd = null,
        [Description("Estimated hours")] double? estimatedHours = null,
        [Description("placeholder | draft | ready (default placeholder)")] string? status = null,
        [Description("[{kind, title?, contentMd}]")] JsonElement? sections = null,
        [Description("[{kind, title, promptMd, referenceMd?, maxScore?, weight?, required?}]")] JsonElement? exercises = null)
        => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var template = await CourseLogic.TemplateAsync(db, course.TemplateId);
        var stage = stageId is Guid sid
            ? await db.Stages.FirstOrDefaultAsync(s => s.Id == sid) ?? throw CourseException.NotFound("stage")
            : stageCode is not null ? await CourseLogic.StageByCodeAsync(db, course.Id, stageCode, track: true)
            : throw CourseException.Validation("stageCode or stageId is required.");

        LessonStatus? want = status is null ? null
            : Enum.TryParse<LessonStatus>(status.Replace("_", ""), true, out var ls) ? ls
            : throw CourseException.Validation($"unknown status '{status}'.");

        var (lesson, created) = await CourseStructure.CreateLessonAsync(db, course, stage, code, title,
            summaryMd, estimatedHours, want is LessonStatus.Ready ? LessonStatus.Draft : want, null, "agent:mcp");
        await db.SaveChangesAsync();

        var madeSections = 0;
        if (sections is JsonElement secs && secs.ValueKind == JsonValueKind.Array)
            foreach (var s in secs.EnumerateArray())
            {
                var kind = s.TryGetProperty("kind", out var k) ? k.GetString() : null;
                if (kind is null) continue;
                await CourseStructure.UpsertSectionAsync(db, course, template, lesson, kind,
                    s.TryGetProperty("title", out var tt) ? tt.GetString() : null,
                    s.TryGetProperty("contentMd", out var c) ? c.GetString() ?? "" : "", "agent:mcp");
                madeSections++;
            }

        var madeExercises = 0;
        if (exercises is JsonElement exs && exs.ValueKind == JsonValueKind.Array)
            foreach (var x in exs.EnumerateArray())
            {
                var kind = x.TryGetProperty("kind", out var k) ? k.GetString() : null;
                var xtitle = x.TryGetProperty("title", out var t2) ? t2.GetString() : null;
                if (kind is null || xtitle is null) continue;
                await CourseStructure.UpsertExerciseAsync(db, course, template, lesson, kind, xtitle,
                    x.TryGetProperty("promptMd", out var pm) ? pm.GetString() ?? "" : "",
                    x.TryGetProperty("referenceMd", out var rm) ? rm.GetString() : null,
                    x.TryGetProperty("maxScore", out var ms) && ms.ValueKind == JsonValueKind.Number ? ms.GetDouble() : null,
                    x.TryGetProperty("weight", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetDouble() : null,
                    x.TryGetProperty("required", out var rq) && rq.ValueKind == JsonValueKind.False ? false : null,
                    null, "agent:mcp");
                madeExercises++;
            }
        await db.SaveChangesAsync();

        // Asking for ready is only honoured once the sections are in, which is why it happens last.
        if (want == LessonStatus.Ready)
        {
            var tracked = await db.Lessons.FirstAsync(l => l.Id == lesson.Id);
            await CourseStructure.SetLessonStatusAsync(db, course, template, tracked, LessonStatus.Ready, false, "agent:mcp");
            await db.SaveChangesAsync();
        }

        return new { created, id = lesson.Id, code = lesson.Code, sections = madeSections, exercises = madeExercises,
            progress = await ProgressEcho(course.Id, lesson.Code) };
    });

    [McpServerTool(Name = "update_lesson"), Description(
        "Change a lesson's title, summary or estimated hours. Omitted arguments are left unchanged. Use " +
        "set_lesson_status for status, upsert_lesson_section for content, move_lesson to change stage.")]
    public async Task<string> UpdateLesson(
        [Description("Course slug")] string courseSlug,
        [Description("Lesson code")] string? code = null,
        [Description("New title")] string? title = null,
        [Description("New Markdown summary")] string? summaryMd = null,
        [Description("New estimated hours")] double? estimatedHours = null,
        [Description("Alias for code")] string? lessonCode = null) => await Run(async () =>
    {
        code = LessonRef(code, lessonCode);
        var course = await CourseRef(null, courseSlug);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, code, track: true);
        if (title is not null) lesson.Title = title;
        if (summaryMd is not null) lesson.SummaryMd = CourseLogic.Md(summaryMd, "summaryMd");
        if (estimatedHours is not null) lesson.EstimatedHours = estimatedHours;
        lesson.UpdatedAt = DateTime.UtcNow;
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, "agent:mcp",
            new { summary = $"lesson {code} updated" }, lesson.StageId, lesson.Id);
        await db.SaveChangesAsync();
        return new { id = lesson.Id, code = lesson.Code, updated = true, progress = await ProgressEcho(course.Id, code) };
    });

    [McpServerTool(Name = "set_lesson_status"), Description(
        "Move a lesson through placeholder -> draft -> ready -> in_progress -> submitted -> completed; anything " +
        "may be skipped, submitted may go back to in_progress, and completed may be reopened.\n\n" +
        "'ready' is CHECKED against the template's required sections and returns the missing kinds if any are " +
        "absent. 'completed' requires a grade on every required exercise; force=true overrides that and the " +
        "override is recorded in the timeline. Lessons also advance on their own as work is submitted.")]
    public async Task<string> SetLessonStatus(
        [Description("Course slug")] string courseSlug,
        [Description("placeholder | draft | ready | in_progress | submitted | completed | skipped")] string status,
        [Description("Lesson code")] string? code = null,
        [Description("Complete even with ungraded required exercises")] bool force = false,
        [Description("Alias for code")] string? lessonCode = null) => await Run(async () =>
    {
        code = LessonRef(code, lessonCode);
        var course = await CourseRef(null, courseSlug);
        var template = await CourseLogic.TemplateAsync(db, course.TemplateId);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, code, track: true);
        if (!Enum.TryParse<LessonStatus>(status.Replace("_", ""), true, out var to))
            throw CourseException.Validation($"unknown status '{status}'.");
        await CourseStructure.SetLessonStatusAsync(db, course, template, lesson, to, force, "agent:mcp");
        await db.SaveChangesAsync();
        return new { code = lesson.Code, status = lesson.Status.Wire(), progress = await ProgressEcho(course.Id, code) };
    });

    [McpServerTool(Name = "reorder_lessons"), Description(
        "Set the order of a stage's lessons. Codes you leave out keep their relative order behind the ones you " +
        "name. Positions come out dense.")]
    public async Task<string> ReorderLessons(
        [Description("Course slug")] string courseSlug,
        [Description("Stage code")] string stageCode,
        [Description("Lesson codes, in the order wanted")] string[] codesInOrder) => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var stage = await CourseLogic.StageByCodeAsync(db, course.Id, stageCode);
        await CourseStructure.ReorderLessonsAsync(db, course, stage, [.. codesInOrder], "agent:mcp");
        await db.SaveChangesAsync();
        var tree = await CourseProgress.ComputeAsync(db, course.Id);
        return new { order = tree.Stages.First(s => s.Code == stageCode).Lessons.Select(l => l.Code) };
    });

    [McpServerTool(Name = "move_lesson"), Description(
        "Move a lesson to a different stage. Its code does not change — codes are unique per course, so the " +
        "lesson keeps its name. Both stages come out with dense positions.")]
    public async Task<string> MoveLesson(
        [Description("Course slug")] string courseSlug,
        [Description("Stage code to move it to")] string toStageCode,
        [Description("Lesson code")] string? code = null,
        [Description("0-based position in the new stage; defaults to the end")] int? position = null,
        [Description("Alias for code")] string? lessonCode = null)
        => await Run(async () =>
    {
        code = LessonRef(code, lessonCode);
        var course = await CourseRef(null, courseSlug);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, code, track: true);
        var stage = await CourseLogic.StageByCodeAsync(db, course.Id, toStageCode, track: true);
        await CourseStructure.MoveLessonAsync(db, course, lesson, stage, position, "agent:mcp");
        await db.SaveChangesAsync();
        return new { code = lesson.Code, stage = stage.Code, progress = await ProgressEcho(course.Id, code) };
    });

    [McpServerTool(Name = "delete_lesson"), Description(
        "Soft-delete a lesson. REFUSES if it has submissions unless force=true.")]
    public async Task<string> DeleteLesson(
        [Description("Course slug")] string courseSlug,
        [Description("Lesson code")] string? code = null,
        [Description("Delete even if it has submissions")] bool force = false,
        [Description("Alias for code")] string? lessonCode = null) => await Run(async () =>
    {
        code = LessonRef(code, lessonCode);
        var course = await CourseRef(null, courseSlug);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, code, track: true);
        await CourseStructure.DeleteLessonAsync(db, course, lesson, force, "agent:mcp");
        await db.SaveChangesAsync();
        return new { deleted = true, code, progress = await ProgressEcho(course.Id) };
    });

    [McpServerTool(Name = "upsert_lesson_section"), Description(
        "Write one section of a lesson. IDEMPOTENT ON (lesson, kind) — calling it again replaces that section's " +
        "Markdown rather than adding a second one, which is why several question groups live as several " +
        "exercises inside the questions section rather than as repeated sections. The kind must be in the course " +
        "template; if it is not, the allowed kinds come back in the error. Markdown is capped at 200 KB.")]
    public async Task<string> UpsertLessonSection(
        [Description("Course slug")] string courseSlug,
        [Description("Lesson code")] string lessonCode,
        [Description("Section kind from the template, e.g. 'theory'")] string kind,
        [Description("Section Markdown")] string contentMd,
        [Description("Heading; defaults to the template's title for this kind")] string? title = null)
        => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var template = await CourseLogic.TemplateAsync(db, course.TemplateId);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, lessonCode, track: true);
        var (section, created) = await CourseStructure.UpsertSectionAsync(db, course, template, lesson, kind,
            title, contentMd, "agent:mcp");
        await db.SaveChangesAsync();
        return new { created, id = section.Id, kind = section.Kind,
            progress = await ProgressEcho(course.Id, lessonCode) };
    });

    [McpServerTool(Name = "delete_lesson_section"), Description(
        "Remove one section of a lesson. Hard delete — sections are cheap to write again. If the lesson is " +
        "'ready' and this removes a required kind, the lesson keeps its status; set it back to draft yourself " +
        "if that is what you mean.")]
    public async Task<string> DeleteLessonSection(
        [Description("Course slug")] string courseSlug,
        [Description("Lesson code")] string lessonCode,
        [Description("Section kind")] string kind) => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, lessonCode);
        var section = await db.LessonSections.FirstOrDefaultAsync(s => s.LessonId == lesson.Id && s.Kind == kind)
            ?? throw CourseException.NotFound($"section {kind}");
        db.LessonSections.Remove(section);
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, "agent:mcp",
            new { summary = $"section {kind} removed from {lessonCode}" }, lesson.StageId, lesson.Id);
        await db.SaveChangesAsync();
        return new { deleted = true, kind };
    });

    [McpServerTool(Name = "create_exercise"), Description(
        "Add a gradeable exercise to a lesson. IDEMPOTENT ON (lesson, kind, title) — a repeat updates the prompt " +
        "rather than creating a twin, so a lesson can hold several exercises of the same kind as long as their " +
        "titles differ. kind must be in the template's exerciseKinds; maxScore and weight default from it, and " +
        "the section it renders under comes from the template too. referenceMd is what a strong answer contains " +
        "— write it, because it is the yardstick when the work is graded later.")]
    public async Task<string> CreateExercise(
        [Description("Course slug")] string courseSlug,
        [Description("Lesson code")] string lessonCode,
        [Description("Exercise kind from the template, e.g. 'prediction'")] string kind,
        [Description("Title, unique within the lesson for this kind")] string title,
        [Description("The prompt, in Markdown")] string promptMd,
        [Description("What a strong answer contains, in Markdown")] string? referenceMd = null,
        [Description("Max score; defaults from the template")] double? maxScore = null,
        [Description("Weight in the lesson score; defaults from the template")] double? weight = null,
        [Description("Optional exercises never block a lesson completing (default true)")] bool? required = null,
        [Description("0-based position; defaults to the end")] int? position = null) => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var template = await CourseLogic.TemplateAsync(db, course.TemplateId);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, lessonCode, track: true);
        var (exercise, created) = await CourseStructure.UpsertExerciseAsync(db, course, template, lesson, kind,
            title, promptMd, referenceMd, maxScore, weight, required, position, "agent:mcp");
        await db.SaveChangesAsync();
        return new { created, id = exercise.Id, title = exercise.Title, sectionKind = exercise.SectionKind,
            maxScore = exercise.MaxScore, weight = exercise.Weight,
            progress = await ProgressEcho(course.Id, lessonCode) };
    });

    [McpServerTool(Name = "update_exercise"), Description(
        "Change an exercise's prompt, reference answer, score, weight or required flag. Address it by id. " +
        "Omitted arguments are left unchanged.")]
    public async Task<string> UpdateExercise(
        [Description("Exercise id")] Guid id,
        [Description("New title")] string? title = null,
        [Description("New prompt Markdown")] string? promptMd = null,
        [Description("New reference Markdown")] string? referenceMd = null,
        [Description("New max score")] double? maxScore = null,
        [Description("New weight")] double? weight = null,
        [Description("New required flag")] bool? required = null) => await Run(async () =>
    {
        var exercise = await db.Exercises.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw CourseException.NotFound("exercise");
        if (title is not null) exercise.Title = title;
        if (promptMd is not null) exercise.PromptMd = CourseLogic.Md(promptMd, "promptMd")!;
        if (referenceMd is not null) exercise.ReferenceMd = CourseLogic.Md(referenceMd, "referenceMd");
        if (maxScore is not null) exercise.MaxScore = maxScore.Value;
        if (weight is not null) exercise.Weight = weight.Value;
        if (required is not null) exercise.Required = required.Value;
        exercise.UpdatedAt = DateTime.UtcNow;
        var courseId = await CourseLogic.CourseIdOfLessonAsync(db, exercise.LessonId);
        CourseLogic.Event(db, courseId, ProgressEventType.StructureChanged, "agent:mcp",
            new { summary = $"exercise '{exercise.Title}' updated" }, null, exercise.LessonId, exercise.Id);
        await db.SaveChangesAsync();
        return new { id = exercise.Id, updated = true };
    });

    [McpServerTool(Name = "reorder_exercises"), Description(
        "Set the order of a lesson's exercises. Titles you leave out keep their relative order behind the ones " +
        "you name.")]
    public async Task<string> ReorderExercises(
        [Description("Course slug")] string courseSlug,
        [Description("Lesson code")] string lessonCode,
        [Description("Exercise titles, in the order wanted")] string[] titlesInOrder) => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, lessonCode);
        var exercises = await db.Exercises.Where(x => x.LessonId == lesson.Id).ToListAsync();
        var missing = titlesInOrder.Where(t => exercises.All(x => x.Title != t)).ToList();
        if (missing.Count > 0) throw CourseException.Validation([.. missing.Select(t => $"unknown exercise '{t}'")]);

        var ordered = titlesInOrder.Select(t => exercises.First(x => x.Title == t))
            .Concat(exercises.Where(x => !titlesInOrder.Contains(x.Title)).OrderBy(x => x.Position)).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i].Position = i;
        CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, "agent:mcp",
            new { summary = $"exercises of {lessonCode} reordered" }, lesson.StageId, lesson.Id);
        await db.SaveChangesAsync();
        return new { order = ordered.Select(x => x.Title) };
    });

    [McpServerTool(Name = "delete_exercise"), Description(
        "Soft-delete an exercise. REFUSES if it has submissions unless force=true.")]
    public async Task<string> DeleteExercise(
        [Description("Exercise id")] Guid id,
        [Description("Delete even if it has submissions")] bool force = false) => await Run(async () =>
    {
        var exercise = await db.Exercises.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw CourseException.NotFound("exercise");
        var courseId = await CourseLogic.CourseIdOfLessonAsync(db, exercise.LessonId);
        var course = await CourseLogic.CourseByRefAsync(db, courseId, null, track: true);
        await CourseStructure.DeleteExerciseAsync(db, course, exercise, force, "agent:mcp");
        await db.SaveChangesAsync();
        return new { deleted = true, id };
    });

    [McpServerTool(Name = "upsert_course_resource"), Description(
        "Attach a link to a course, or to one lesson of it. IDEMPOTENT ON URL within the course. kind is one of " +
        "doc|paper|blog|repo|video|book|other. (Not in the original spec's tool list, which only let resources in " +
        "through import_course_outline — this is the same thing for a single link.)")]
    public async Task<string> UpsertCourseResource(
        [Description("Course slug")] string courseSlug,
        [Description("Link title")] string title,
        [Description("URL")] string url,
        [Description("doc | paper | blog | repo | video | book | other")] string kind = "other",
        [Description("Why it is worth keeping, in Markdown")] string? noteMd = null,
        [Description("Attach to this lesson instead of the course as a whole")] string? lessonCode = null)
        => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        Guid? lessonId = lessonCode is null ? null
            : (await CourseLogic.LessonByCodeAsync(db, course.Id, lessonCode)).Id;
        var existing = await db.CourseResources.FirstOrDefaultAsync(r => r.CourseId == course.Id && r.Url == url);
        if (existing is not null)
        {
            existing.Title = title; existing.Kind = kind; existing.NoteMd = noteMd ?? existing.NoteMd;
            existing.LessonId = lessonId ?? existing.LessonId;
            await db.SaveChangesAsync();
            return new { created = false, id = existing.Id };
        }
        var max = await db.CourseResources.Where(r => r.CourseId == course.Id)
            .Select(r => (int?)r.Position).MaxAsync() ?? -1;
        var res = new CourseResource
        {
            Id = Guid.NewGuid(), CourseId = course.Id, LessonId = lessonId, Title = title, Url = url,
            Kind = kind, NoteMd = noteMd, Position = max + 1,
        };
        db.CourseResources.Add(res);
        await db.SaveChangesAsync();
        return new { created = true, id = res.Id };
    });

    [McpServerTool(Name = "delete_course_resource"), Description("Remove a link from a course. Hard delete.")]
    public async Task<string> DeleteCourseResource(
        [Description("Course slug")] string courseSlug,
        [Description("The URL to remove")] string url) => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var res = await db.CourseResources.FirstOrDefaultAsync(r => r.CourseId == course.Id && r.Url == url)
            ?? throw CourseException.NotFound("resource");
        db.CourseResources.Remove(res);
        await db.SaveChangesAsync();
        return new { deleted = true };
    });

    // ==================== 6.3 Work and grading ====================

    [McpServerTool(Name = "add_submission"), Description(
        "Record an attempt at an exercise. APPEND-ONLY: a second call is attempt 2, never an edit, so the record " +
        "of how the work went is not rewritten by how it ended. Returns the submission with its attemptNo.\n\n" +
        "Side effects: a 'ready' lesson moves to 'in_progress', and once every required exercise has at least " +
        "one submission the lesson moves to 'submitted'. Both are recorded with actor 'system'.\n\n" +
        "links is [{label, url, kind}] with kind repo|commit|pr|file|doc — this is where work that lives outside " +
        "the app is pointed at. Pass idempotencyKey if you might retry; the same key returns the first submission " +
        "instead of making another attempt.")]
    public async Task<string> AddSubmission(
        [Description("Exercise id (or give courseSlug + lessonCode + exerciseTitle)")] Guid? exerciseId = null,
        [Description("Course slug")] string? courseSlug = null,
        [Description("Lesson code")] string? lessonCode = null,
        [Description("Exercise title")] string? exerciseTitle = null,
        [Description("The answer or a summary of the work, in Markdown")] string? contentMd = null,
        [Description("[{label, url, kind}] — repo|commit|pr|file|doc, at most 50")] JsonElement? links = null,
        [Description("'agent:claude-code' or 'user'")] string submittedBy = "agent:claude-code",
        [Description("Retry guard; the same key returns the first submission")] string? idempotencyKey = null)
        => await Run(async () =>
    {
        var (course, exercise) = await ExerciseRef(exerciseId, courseSlug, lessonCode, exerciseTitle);
        var submission = await CourseWork.AddSubmissionAsync(db, course, exercise, contentMd,
            links is JsonElement l && l.ValueKind == JsonValueKind.Array ? l.GetRawText() : null,
            submittedBy, idempotencyKey);
        var lesson = await db.Lessons.AsNoTracking().FirstAsync(x => x.Id == exercise.LessonId);
        return new
        {
            id = submission.Id, attemptNo = submission.AttemptNo, exerciseTitle = exercise.Title,
            progress = await ProgressEcho(course.Id, lesson.Code),
        };
    });

    [McpServerTool(Name = "grade_submission"), Description(
        "Mark one submission. If it already has a grade this writes a SUPERSEDING grade rather than overwriting, " +
        "so a change of mind stays legible; the lesson score then uses whichever the course's metadata.scoring " +
        "says ('best' by default, 'latest' if set). score must be between 0 and maxScore, which defaults to the " +
        "exercise's own. rubric is an optional [{criterion, score, max, note}] breakdown. Returns the grade and " +
        "the recomputed lesson score.")]
    public async Task<string> GradeSubmission(
        [Description("Submission id")] Guid submissionId,
        [Description("Score, 0..maxScore")] double score,
        [Description("Feedback in Markdown")] string? feedbackMd = null,
        [Description("Overrides the exercise's max score")] double? maxScore = null,
        [Description("[{criterion, score, max, note}]")] JsonElement? rubric = null,
        [Description("'agent:claude-code' or 'user'")] string gradedBy = "agent:claude-code",
        [Description("Retry guard")] string? idempotencyKey = null) => await Run(async () =>
    {
        var submission = await db.Submissions.FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw CourseException.NotFound("submission");
        var courseId = await CourseLogic.CourseIdOfLessonAsync(db,
            await db.Exercises.Where(x => x.Id == submission.ExerciseId).Select(x => x.LessonId).FirstAsync());
        var course = await CourseLogic.CourseByRefAsync(db, courseId, null);
        var grade = await CourseWork.GradeAsync(db, course, submission, score, maxScore, feedbackMd,
            rubric is JsonElement r && r.ValueKind == JsonValueKind.Array ? r.GetRawText() : null,
            gradedBy, idempotencyKey);
        var lessonCode = await db.Lessons.AsNoTracking()
            .Where(l => l.Exercises.Any(x => x.Id == submission.ExerciseId)).Select(l => l.Code).FirstOrDefaultAsync();
        return new { id = grade.Id, score = grade.Score, maxScore = grade.MaxScore,
            supersedes = grade.SupersedesGradeId, progress = await ProgressEcho(course.Id, lessonCode) };
    });

    [McpServerTool(Name = "grade_latest"), Description(
        "Grade the LATEST submission of several exercises of one lesson in a single call — the normal way to mark " +
        "a lesson's work once. grades is [{exerciseTitle, score, feedbackMd?, rubric?, maxScore?}]. Exercises " +
        "with no submission yet are reported in 'skipped' rather than failing the call.")]
    public async Task<string> GradeLatest(
        [Description("Course slug")] string courseSlug,
        [Description("Lesson code")] string lessonCode,
        [Description("[{exerciseTitle, score, feedbackMd?, rubric?, maxScore?}]")] JsonElement grades,
        [Description("'agent:claude-code' or 'user'")] string gradedBy = "agent:claude-code")
        => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, lessonCode);
        if (grades.ValueKind != JsonValueKind.Array)
            throw CourseException.Validation("grades must be an array.");

        var done = new List<object>();
        var skipped = new List<string>();
        foreach (var g in grades.EnumerateArray())
        {
            var title = g.TryGetProperty("exerciseTitle", out var t) ? t.GetString() : null;
            if (title is null) { skipped.Add("(entry with no exerciseTitle)"); continue; }
            var exercise = await db.Exercises.FirstOrDefaultAsync(x => x.LessonId == lesson.Id && x.Title == title);
            if (exercise is null) { skipped.Add($"{title}: no such exercise"); continue; }

            var submission = await db.Submissions.Where(s => s.ExerciseId == exercise.Id)
                .OrderByDescending(s => s.AttemptNo).FirstOrDefaultAsync();
            if (submission is null) { skipped.Add($"{title}: no submission yet"); continue; }

            var grade = await CourseWork.GradeAsync(db, course, submission,
                g.TryGetProperty("score", out var sc) ? sc.GetDouble() : 0,
                g.TryGetProperty("maxScore", out var ms) && ms.ValueKind == JsonValueKind.Number ? ms.GetDouble() : null,
                g.TryGetProperty("feedbackMd", out var fb) ? fb.GetString() : null,
                g.TryGetProperty("rubric", out var rb) && rb.ValueKind == JsonValueKind.Array ? rb.GetRawText() : null,
                gradedBy, null);
            done.Add(new { exerciseTitle = title, score = grade.Score, maxScore = grade.MaxScore });
        }
        return new { graded = done, skipped, progress = await ProgressEcho(course.Id, lessonCode) };
    });

    [McpServerTool(Name = "log_course_event"), Description(
        "Write a note or a handoff onto the course timeline.\n\n" +
        "type='handoff' with payload {md} is THE ONE TO WRITE AT THE END OF A SESSION: it is what " +
        "get_course_resume hands back first, so say what you stopped in the middle of and what comes next " +
        "('stopped after break-it #2; next: capstone increment').\n" +
        "type='note' with payload {md} is an ordinary remark.")]
    public async Task<string> LogCourseEvent(
        [Description("Course slug")] string courseSlug,
        [Description("note | handoff")] string type,
        [Description("{md}")] JsonElement payload,
        [Description("Attach it to a lesson")] string? lessonCode = null,
        [Description("'agent:claude-code' or 'user'")] string actor = "agent:claude-code") => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var t = CourseWork.ParseType(type) ?? throw CourseException.Validation($"unknown event type '{type}'.");
        if (t is not (ProgressEventType.Note or ProgressEventType.Handoff))
            throw CourseException.Validation("type must be note or handoff; the rest are written by the app.");

        Guid? stageId = null, lessonId = null;
        if (lessonCode is not null)
        {
            var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, lessonCode);
            lessonId = lesson.Id; stageId = lesson.StageId;
        }
        if (t is ProgressEventType.Note or ProgressEventType.Handoff
            && payload.TryGetProperty("md", out var md))
            CourseLogic.Md(md.GetString(), "payload.md");

        var ev = CourseLogic.Event(db, course.Id, t, actor,
            JsonSerializer.Deserialize<JsonElement>(payload.GetRawText()), stageId, lessonId);
        await db.SaveChangesAsync();
        return new { id = ev.Id, type = CourseWork.Wire(t), createdAt = ev.CreatedAt };
    });

    [McpServerTool(Name = "get_course_resume"), Description(
        "WHERE ARE WE — call this FIRST when picking a course back up. One call returns the current stage and " +
        "lesson, which of that lesson's exercises are still waiting on a submission or a grade, the last handoff " +
        "note the previous session left, the last ten grades, the last twenty timeline events, and — when " +
        "nothing is in flight — the next stage that still has undefined lessons, so there is always an obvious " +
        "next move.")]
    public async Task<string> GetCourseResume(
        [Description("Course slug")] string courseSlug) => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        return await CourseWork.ResumeAsync(db, course);
    });

    [McpServerTool(Name = "get_lesson"), Description(
        "One lesson in full — what to load when resuming work on it. include controls how much comes back: any " +
        "of 'sections', 'exercises', 'submissions', 'grades' (default: sections and exercises). Sections come " +
        "back in template order. With 'submissions' each exercise carries its attempts; with 'grades' each " +
        "attempt carries its marks, superseded ones included so the history is visible.")]
    public async Task<string> GetLesson(
        [Description("Course slug")] string courseSlug,
        [Description("Lesson code")] string lessonCode,
        [Description("Any of: sections, exercises, submissions, grades")] string[]? include = null)
        => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, lessonCode);
        return await CourseViews.LessonDetailAsync(db, course, lesson, CourseViews.LessonInclude(include));
    });

    [McpServerTool(Name = "get_course_timeline"), Description(
        "The course's timeline, newest first. Cursor-paginate by passing the id of the last event you saw. " +
        "types filters to any of status_changed, submission_added, grade_added, note, handoff, " +
        "structure_changed.")]
    public async Task<string> GetCourseTimeline(
        [Description("Course slug")] string courseSlug,
        [Description("Return events older than this event id")] long? cursor = null,
        [Description("How many (default 50, max 200)")] int limit = 50,
        [Description("Filter to these event types")] string[]? types = null) => await Run(async () =>
    {
        var course = await CourseRef(null, courseSlug);
        var events = await CourseWork.TimelineAsync(db, course.Id, cursor, limit, types);
        return new { events, nextCursor = events.Count == 0 ? null : (long?)((dynamic)events[^1]).id };
    });

    // ==================== shared lookups ====================

    private async Task<(Course, Stage)> StageRef(Guid? id, string? courseSlug, string? code, bool track)
    {
        if (id is Guid sid)
        {
            var stage = (track ? db.Stages : db.Stages.AsNoTracking()).FirstOrDefault(s => s.Id == sid)
                ?? throw CourseException.NotFound("stage");
            return (await CourseLogic.CourseByRefAsync(db, stage.CourseId, null, track), stage);
        }
        if (courseSlug is null || code is null)
            throw CourseException.Validation("pass a stage id, or courseSlug and code.");
        var course = await CourseLogic.CourseByRefAsync(db, null, courseSlug, track);
        return (course, await CourseLogic.StageByCodeAsync(db, course.Id, code, track));
    }

    private async Task<(Course, Exercise)> ExerciseRef(Guid? exerciseId, string? courseSlug, string? lessonCode,
        string? exerciseTitle)
    {
        if (exerciseId is Guid xid)
        {
            var exercise = await db.Exercises.FirstOrDefaultAsync(x => x.Id == xid)
                ?? throw CourseException.NotFound("exercise");
            var courseId = await CourseLogic.CourseIdOfLessonAsync(db, exercise.LessonId);
            return (await CourseLogic.CourseByRefAsync(db, courseId, null), exercise);
        }
        if (courseSlug is null || lessonCode is null || exerciseTitle is null)
            throw CourseException.Validation("pass an exerciseId, or courseSlug + lessonCode + exerciseTitle.");
        var course = await CourseLogic.CourseByRefAsync(db, null, courseSlug);
        var lesson = await CourseLogic.LessonByCodeAsync(db, course.Id, lessonCode);
        var byTitle = await db.Exercises.FirstOrDefaultAsync(x => x.LessonId == lesson.Id && x.Title == exerciseTitle)
            ?? throw CourseException.NotFound($"exercise '{exerciseTitle}'");
        return (course, byTitle);
    }
}
