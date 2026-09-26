using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Courses;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Endpoints;

/// <summary>
/// The Courses tab's read side, plus the two things a person changes by hand.
///
/// Reads come straight from <see cref="CourseViews"/> — the same projections the MCP tools serve,
/// so the tab and the authoring agent can never disagree about a course. Writes are deliberately
/// narrow: a course status, a lesson status, and a note or a time log. Everything else about a
/// course is written by the agent over MCP, because writing a lesson means writing Markdown and
/// that is not a form.
/// </summary>
public static class CourseEndpoints
{
    public static void MapCourseEndpoints(this WebApplication app)
    {
        var courses = app.MapGroup("/api/courses").WithTags("Courses").RequireAuthorization();

        courses.MapGet("/", async (string? status, RoadmapDbContext db) => await Guard(async () =>
        {
            CourseStatus? filter = status is not null
                && Enum.TryParse<CourseStatus>(status.Replace("_", ""), true, out var st) ? st : null;
            return Results.Ok(await CourseViews.SummariesAsync(db, filter));
        }));

        // {course} is an id or a slug: the tab navigates by slug (they are in the URL a person
        // reads), the agent usually holds an id, and neither should have to translate.
        courses.MapGet("/{course}", async (string course, string? depth, RoadmapDbContext db) =>
            await Guard(async () => Results.Ok(
                await CourseViews.DetailAsync(db, await Resolve(db, course), depth ?? "lessons"))));

        courses.MapGet("/{course}/lessons/{lesson}", async (string course, string lesson,
            string? include, RoadmapDbContext db) => await Guard(async () =>
        {
            var c = await Resolve(db, course);
            var l = await ResolveLesson(db, c, lesson);
            var want = CourseViews.LessonInclude(include?.Split(',', StringSplitOptions.RemoveEmptyEntries));
            return Results.Ok(await CourseViews.LessonDetailAsync(db, c, l, want));
        }));

        courses.MapGet("/{course}/timeline", async (string course, long? cursor, int? limit,
            string? types, RoadmapDbContext db) => await Guard(async () =>
        {
            var c = await Resolve(db, course);
            var rows = await CourseWork.TimelineAsync(db, c.Id, cursor, limit ?? 50,
                types?.Split(',', StringSplitOptions.RemoveEmptyEntries));
            return Results.Ok(rows);
        }));

        courses.MapGet("/{course}/resume", async (string course, RoadmapDbContext db) =>
            await Guard(async () => Results.Ok(await CourseWork.ResumeAsync(db, await Resolve(db, course)))));

        courses.MapPatch("/{course}/status", async (string course, SetStatusRequest req,
            RoadmapDbContext db) => await Guard(async () =>
        {
            var c = await Resolve(db, course, track: true);
            if (!Enum.TryParse<CourseStatus>((req.Status ?? "").Replace("_", ""), true, out var to))
                throw CourseException.Validation($"unknown status '{req.Status}'.");
            CourseStructure.SetCourseStatus(db, c, to, "user");
            await db.SaveChangesAsync();
            return Results.Ok(await CourseViews.DetailAsync(db, c, "lessons"));
        }));

        courses.MapPatch("/{course}/lessons/{lesson}/status", async (string course, string lesson,
            SetStatusRequest req, RoadmapDbContext db) => await Guard(async () =>
        {
            var c = await Resolve(db, course);
            var l = await ResolveLesson(db, c, lesson, track: true);
            var template = await CourseLogic.TemplateAsync(db, c.TemplateId);
            if (!Enum.TryParse<LessonStatus>((req.Status ?? "").Replace("_", ""), true, out var to))
                throw CourseException.Validation($"unknown status '{req.Status}'.");
            await CourseStructure.SetLessonStatusAsync(db, c, template, l, to, req.Force ?? false, "user");
            await db.SaveChangesAsync();
            return Results.Ok(await CourseViews.LessonDetailAsync(db, c, l,
                CourseViews.LessonInclude(null)));
        }));

        // The only user-originated write besides the two statuses.
        courses.MapPost("/{course}/events", async (string course, CourseEventRequest req,
            RoadmapDbContext db) => await Guard(async () =>
        {
            var c = await Resolve(db, course);
            var type = CourseWork.ParseType(req.Type)
                ?? throw CourseException.Validation($"unknown event type '{req.Type}'.");
            if (type is not ProgressEventType.Note)
                throw CourseException.Validation("only 'note' may be posted from the app.");

            Guid? stageId = null, lessonId = null;
            if (!string.IsNullOrWhiteSpace(req.LessonCode))
            {
                var l = await ResolveLesson(db, c, req.LessonCode!);
                (stageId, lessonId) = (l.StageId, l.Id);
            }
            var ev = CourseLogic.Event(db, c.Id, type, "user", req.Payload ?? new { }, stageId, lessonId);
            await db.SaveChangesAsync();
            return Results.Ok(new { id = ev.Id, type = type.Wire(), createdAt = ev.CreatedAt });
        }));
    }

    private static async Task<Course> Resolve(RoadmapDbContext db, string courseRef, bool track = false) =>
        Guid.TryParse(courseRef, out var id)
            ? await CourseLogic.CourseByRefAsync(db, id, null, track)
            : await CourseLogic.CourseByRefAsync(db, null, courseRef, track);

    private static async Task<Lesson> ResolveLesson(RoadmapDbContext db, Course course, string lessonRef,
        bool track = false)
    {
        if (!Guid.TryParse(lessonRef, out var id))
            return await CourseLogic.LessonByCodeAsync(db, course.Id, lessonRef, track);
        var q = track ? db.Lessons : db.Lessons.AsNoTracking();
        return await q.FirstOrDefaultAsync(l => l.Id == id && l.Stage.CourseId == course.Id)
            ?? throw CourseException.NotFound($"lesson {lessonRef}");
    }

    /// <summary>
    /// The status codes §5 asks for: 409 for a move the status machine refuses, 422 for a template
    /// violation (so the tab can list the missing section kinds), 404 and 400 for the rest. The
    /// body is the same object the MCP tools return, so one error shape serves both surfaces.
    /// </summary>
    private static async Task<IResult> Guard(Func<Task<IResult>> body)
    {
        try { return await body(); }
        catch (CourseException ex)
        {
            return ex.Kind switch
            {
                "not_found" => Results.NotFound(ex.Payload),
                "invalid_transition" => Results.Json(ex.Payload, statusCode: StatusCodes.Status409Conflict),
                "template" => Results.Json(ex.Payload, statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => Results.BadRequest(ex.Payload),
            };
        }
    }
}

public record SetStatusRequest(string? Status, bool? Force);
public record CourseEventRequest(string? Type, object? Payload, string? LessonCode);
