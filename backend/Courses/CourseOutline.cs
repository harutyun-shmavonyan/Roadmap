using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Courses;

/// <summary>
/// Turning one JSON document into a whole course, and doing it again later without damage.
///
/// Upserts on natural keys — course <c>slug</c>, stage <c>code</c>, lesson <c>code</c>, section
/// <c>kind</c>, exercise <c>(kind, title)</c> — and never deletes. A field the document leaves
/// out is left alone rather than cleared, which is what lets a second, fuller import fill a
/// course in rather than replace it. Running the same document twice reports everything as
/// unchanged.
/// </summary>
public static class CourseOutline
{
    private sealed class Counts
    {
        public int Created, Updated, Unchanged;
        public void Add(bool created, bool changed)
        {
            if (created) Created++;
            else if (changed) Updated++;
            else Unchanged++;
        }
    }

    private static string? Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind is not JsonValueKind.Null ? v.GetString() : null;

    private static double? Num(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.Number ? v.GetDouble() : null;

    private static bool Has(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind is not JsonValueKind.Null;

    /// <summary>Assign only when it differs, and report whether it did — that is what makes the
    /// second import of the same document report "unchanged" rather than "updated".</summary>
    private static bool Set<T>(T current, T next, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, next)) return false;
        assign(next);
        return true;
    }

    /// <summary>The same, for a jsonb column, where the text we wrote is not the text we read back.</summary>
    private static bool SetJson(string? current, string next, Action<string> assign)
    {
        if (CourseLogic.JsonEquivalent(current, next)) return false;
        assign(next);
        return true;
    }

    public static async Task<object> ImportAsync(RoadmapDbContext db, JsonElement outline, string actor)
    {
        if (outline.ValueKind != JsonValueKind.Object)
            throw CourseException.Validation("outline must be a JSON object.");
        if (!outline.TryGetProperty("course", out var courseEl) || courseEl.ValueKind != JsonValueKind.Object)
            throw CourseException.Validation("outline.course is required.");

        var slug = Str(courseEl, "slug") ?? throw CourseException.Validation("course.slug is required.");
        var errors = new List<string>();

        // --- template: created if given and absent, otherwise resolved by name ---
        var template = await ResolveTemplateAsync(db, outline, courseEl, errors);

        // --- course ---
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Slug == slug);
        var courseCreated = course is null;
        if (course is null)
        {
            course = new Course
            {
                Id = Guid.NewGuid(), Slug = slug,
                Title = Str(courseEl, "title") ?? slug,
                TemplateId = template.Id,
            };
            db.Courses.Add(course);
        }

        var courseChanged = false;
        if (Has(courseEl, "title")) courseChanged |= Set(course.Title, Str(courseEl, "title")!, v => course.Title = v);
        if (Has(courseEl, "subtitle")) courseChanged |= Set(course.Subtitle, Str(courseEl, "subtitle"), v => course.Subtitle = v);
        if (Has(courseEl, "descriptionMd")) courseChanged |= Set(course.DescriptionMd, CourseLogic.Md(Str(courseEl, "descriptionMd"), "descriptionMd"), v => course.DescriptionMd = v);
        if (Has(courseEl, "capstoneMd")) courseChanged |= Set(course.CapstoneMd, CourseLogic.Md(Str(courseEl, "capstoneMd"), "capstoneMd"), v => course.CapstoneMd = v);
        if (Has(courseEl, "instructionsMd")) courseChanged |= Set(course.InstructionsMd, CourseLogic.Md(Str(courseEl, "instructionsMd"), "instructionsMd"), v => course.InstructionsMd = v);
        if (Has(courseEl, "targetHoursPerWeek")) courseChanged |= Set(course.TargetHoursPerWeek, Num(courseEl, "targetHoursPerWeek"), v => course.TargetHoursPerWeek = v);
        if (Has(courseEl, "metadata")) courseChanged |= SetJson(course.Metadata,
            courseEl.GetProperty("metadata").GetRawText(), v => course.Metadata = v);
        if (course.TemplateId != template.Id) { course.TemplateId = template.Id; courseChanged = true; }
        if (courseChanged && !courseCreated) course.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var stageCounts = new Counts();
        var lessonCounts = new Counts();
        var sectionCounts = new Counts();
        var exerciseCounts = new Counts();
        var resourceCounts = new Counts();

        // --- stages, and everything under them ---
        if (outline.TryGetProperty("stages", out var stagesEl) && stagesEl.ValueKind == JsonValueKind.Array)
        {
            var order = 0;
            foreach (var stageEl in stagesEl.EnumerateArray())
            {
                var code = Str(stageEl, "code");
                if (code is null) { errors.Add("a stage has no code; skipped."); continue; }

                var (stage, created) = await CourseStructure.CreateStageAsync(db, course, code,
                    Str(stageEl, "title") ?? code, Str(stageEl, "summaryMd"), Num(stageEl, "targetWeeks"),
                    order, actor);
                var changed = false;
                if (!created)
                {
                    if (Has(stageEl, "title")) changed |= Set(stage.Title, Str(stageEl, "title")!, v => stage.Title = v);
                    if (Has(stageEl, "summaryMd")) changed |= Set(stage.SummaryMd, CourseLogic.Md(Str(stageEl, "summaryMd"), "summaryMd"), v => stage.SummaryMd = v);
                    if (Has(stageEl, "targetWeeks")) changed |= Set(stage.TargetWeeks, Num(stageEl, "targetWeeks"), v => stage.TargetWeeks = v);
                    changed |= Set(stage.Position, order, v => stage.Position = v);
                    if (Has(stageEl, "status") && Enum.TryParse<StageStatus>(Str(stageEl, "status")?.Replace("_", ""), true, out var sst)
                        && stage.Status == StageStatus.Planned && sst != StageStatus.Planned)
                    {
                        stage.Status = sst;
                        changed = true;
                    }
                    if (changed) stage.UpdatedAt = DateTime.UtcNow;
                }
                stageCounts.Add(created, changed);
                order++;
                await db.SaveChangesAsync();

                if (!stageEl.TryGetProperty("lessons", out var lessonsEl) || lessonsEl.ValueKind != JsonValueKind.Array)
                    continue;

                var lessonOrder = 0;
                foreach (var lessonEl in lessonsEl.EnumerateArray())
                {
                    await ImportLessonAsync(db, course, template, stage, lessonEl, lessonOrder++, actor,
                        lessonCounts, sectionCounts, exerciseCounts, errors);
                }
                await db.SaveChangesAsync();
            }
        }

        // --- resources, course-level unless they name a lesson ---
        if (outline.TryGetProperty("resources", out var resEl) && resEl.ValueKind == JsonValueKind.Array)
        {
            var pos = 0;
            foreach (var r in resEl.EnumerateArray())
            {
                var url = Str(r, "url");
                var title = Str(r, "title");
                if (url is null || title is null) { errors.Add("a resource is missing title or url; skipped."); continue; }

                Guid? lessonId = null;
                if (Str(r, "lessonCode") is string lc)
                {
                    var l = await db.Lessons.FirstOrDefaultAsync(x => x.Code == lc && x.Stage.CourseId == course.Id);
                    if (l is null) errors.Add($"resource references unknown lesson {lc}; attached to the course instead.");
                    lessonId = l?.Id;
                }

                var existing = await db.CourseResources
                    .FirstOrDefaultAsync(x => x.CourseId == course.Id && x.Url == url);
                if (existing is null)
                {
                    db.CourseResources.Add(new CourseResource
                    {
                        Id = Guid.NewGuid(), CourseId = course.Id, LessonId = lessonId, Title = title,
                        Url = url, Kind = Str(r, "kind") ?? "other", NoteMd = Str(r, "noteMd"), Position = pos,
                    });
                    resourceCounts.Add(true, false);
                }
                else
                {
                    var changed = Set(existing.Title, title, v => existing.Title = v);
                    changed |= Set(existing.Kind, Str(r, "kind") ?? existing.Kind, v => existing.Kind = v);
                    if (Has(r, "noteMd")) changed |= Set(existing.NoteMd, Str(r, "noteMd"), v => existing.NoteMd = v);
                    changed |= Set(existing.LessonId, lessonId ?? existing.LessonId, v => existing.LessonId = v);
                    resourceCounts.Add(false, changed);
                }
                pos++;
            }
        }

        // A re-import that changed nothing says nothing: the timeline is a record of what happened
        // to the course, and "someone ran the importer again" did not happen to it.
        var touched = courseCreated || courseChanged || new[] { stageCounts, lessonCounts, sectionCounts,
            exerciseCounts, resourceCounts }.Any(c => c.Created > 0 || c.Updated > 0);
        if (touched)
            CourseLogic.Event(db, course.Id, ProgressEventType.StructureChanged, actor, new
            {
                summary = courseCreated ? $"course {slug} imported" : $"course {slug} re-imported",
                stages = new { stageCounts.Created, stageCounts.Updated, stageCounts.Unchanged },
                lessons = new { lessonCounts.Created, lessonCounts.Updated, lessonCounts.Unchanged },
            });
        await db.SaveChangesAsync();

        var progress = await CourseProgress.ComputeAsync(db, course.Id);
        return new
        {
            course = new { id = course.Id, slug = course.Slug, title = course.Title, created = courseCreated },
            template = new { id = template.Id, name = template.Name },
            stages = Report(stageCounts),
            lessons = Report(lessonCounts),
            sections = Report(sectionCounts),
            exercises = Report(exerciseCounts),
            resources = Report(resourceCounts),
            errors,
            progress = new { progress.Progress, progress.DefinedFraction },
        };
    }

    private static object Report(Counts c) => new { created = c.Created, updated = c.Updated, unchanged = c.Unchanged };

    private static async Task<LessonTemplate> ResolveTemplateAsync(RoadmapDbContext db, JsonElement outline,
        JsonElement courseEl, List<string> errors)
    {
        var name = Str(courseEl, "templateName");
        JsonElement tEl = default;
        var hasInline = outline.TryGetProperty("template", out tEl) && tEl.ValueKind == JsonValueKind.Object;
        if (hasInline) name ??= Str(tEl, "name");
        if (name is null) throw CourseException.Validation("course.templateName or template.name is required.");

        var template = await db.LessonTemplates.FirstOrDefaultAsync(t => t.Name == name);
        if (template is null)
        {
            if (!hasInline)
                throw CourseException.Validation($"template '{name}' does not exist and no inline template was given.");
            template = new LessonTemplate { Id = Guid.NewGuid(), Name = name };
            db.LessonTemplates.Add(template);
        }

        if (hasInline)
        {
            var changed = false;
            if (Has(tEl, "description"))
                changed |= Set(template.Description, Str(tEl, "description"), v => template.Description = v);
            if (tEl.TryGetProperty("sections", out var secs) && secs.ValueKind == JsonValueKind.Array)
                changed |= SetJson(template.Sections, secs.GetRawText(), v => template.Sections = v);
            if (tEl.TryGetProperty("exerciseKinds", out var kinds) && kinds.ValueKind == JsonValueKind.Array)
                changed |= SetJson(template.ExerciseKinds, kinds.GetRawText(), v => template.ExerciseKinds = v);
            if (changed) template.UpdatedAt = DateTime.UtcNow;
        }

        if (CourseLogic.Sections(template).Count == 0)
            errors.Add($"template '{name}' has no sections; lessons can never reach ready.");
        await db.SaveChangesAsync();
        return template;
    }

    private static async Task ImportLessonAsync(RoadmapDbContext db, Course course, LessonTemplate template,
        Stage stage, JsonElement lessonEl, int order, string actor,
        Counts lessons, Counts sections, Counts exercises, List<string> errors)
    {
        var code = Str(lessonEl, "code");
        if (code is null) { errors.Add($"a lesson in stage {stage.Code} has no code; skipped."); return; }

        var (lesson, created) = await CourseStructure.CreateLessonAsync(db, course, stage, code,
            Str(lessonEl, "title") ?? code, Str(lessonEl, "summaryMd"), Num(lessonEl, "estimatedHours"),
            ParseLessonStatus(Str(lessonEl, "status")), order, actor);

        var changed = false;
        if (!created)
        {
            if (Has(lessonEl, "title")) changed |= Set(lesson.Title, Str(lessonEl, "title")!, v => lesson.Title = v);
            if (Has(lessonEl, "summaryMd")) changed |= Set(lesson.SummaryMd, CourseLogic.Md(Str(lessonEl, "summaryMd"), "summaryMd"), v => lesson.SummaryMd = v);
            if (Has(lessonEl, "estimatedHours")) changed |= Set(lesson.EstimatedHours, Num(lessonEl, "estimatedHours"), v => lesson.EstimatedHours = v);
            if (lesson.StageId == stage.Id) changed |= Set(lesson.Position, order, v => lesson.Position = v);

            // An import may move a lesson forward out of placeholder/draft, never backwards, and
            // never over work that has already been done.
            if (ParseLessonStatus(Str(lessonEl, "status")) is LessonStatus want
                && lesson.Status is LessonStatus.Placeholder or LessonStatus.Draft
                && want > lesson.Status)
            {
                var hasWork = await db.Submissions.AnyAsync(s => s.Exercise.LessonId == lesson.Id);
                if (!hasWork) { lesson.Status = want; changed = true; }
            }
            if (changed) lesson.UpdatedAt = DateTime.UtcNow;
        }
        lessons.Add(created, changed);
        await db.SaveChangesAsync();

        if (lessonEl.TryGetProperty("sections", out var secs) && secs.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in secs.EnumerateArray())
            {
                var kind = Str(s, "kind");
                if (kind is null) { errors.Add($"a section of lesson {code} has no kind; skipped."); continue; }
                try
                {
                    var before = await db.LessonSections.AsNoTracking()
                        .Where(x => x.LessonId == lesson.Id && x.Kind == kind)
                        .Select(x => x.ContentMd).FirstOrDefaultAsync();
                    var content = Str(s, "contentMd") ?? "";
                    var (_, madeNew) = await CourseStructure.UpsertSectionAsync(db, course, template, lesson,
                        kind, Str(s, "title"), content, actor);
                    sections.Add(madeNew, !madeNew && before != content);
                }
                catch (CourseException ex) { errors.Add($"lesson {code} section {kind}: {ex.Kind}"); }
            }
        }

        if (lessonEl.TryGetProperty("exercises", out var exs) && exs.ValueKind == JsonValueKind.Array)
        {
            var pos = 0;
            foreach (var x in exs.EnumerateArray())
            {
                var kind = Str(x, "kind");
                var title = Str(x, "title");
                if (kind is null || title is null)
                {
                    errors.Add($"an exercise of lesson {code} is missing kind or title; skipped.");
                    continue;
                }
                try
                {
                    var before = await db.Exercises.AsNoTracking()
                        .Where(e => e.LessonId == lesson.Id && e.Kind == kind && e.Title == title)
                        .Select(e => new { e.PromptMd, e.ReferenceMd }).FirstOrDefaultAsync();
                    var prompt = Str(x, "promptMd") ?? "";
                    var (_, madeNew) = await CourseStructure.UpsertExerciseAsync(db, course, template, lesson,
                        kind, title, prompt, Str(x, "referenceMd"), Num(x, "maxScore"), Num(x, "weight"),
                        x.TryGetProperty("required", out var rq) && rq.ValueKind == JsonValueKind.False ? false : null,
                        pos, actor);
                    exercises.Add(madeNew, !madeNew && before is not null
                        && (before.PromptMd != prompt
                            || (Str(x, "referenceMd") is string rm && before.ReferenceMd != rm)));
                }
                catch (CourseException ex) { errors.Add($"lesson {code} exercise '{title}': {ex.Kind}"); }
                pos++;
            }
        }
        await db.SaveChangesAsync();
    }

    private static LessonStatus? ParseLessonStatus(string? s) =>
        s is null ? null
        : Enum.TryParse<LessonStatus>(s.Replace("_", ""), true, out var v) ? v : null;
}
