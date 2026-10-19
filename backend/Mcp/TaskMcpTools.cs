using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

/// <summary>
/// The Tasks tab over MCP: a roadmap's single (one-off) tasks, the projects that group them and the
/// dependencies between them. Same rows and the same rules as /api/roadmaps/{id}/tasks, through
/// <see cref="TaskLogic"/>. <c>update_task</c> and <c>update_task_project</c> patch — a null
/// argument keeps what is stored and an empty string clears a field — because an assistant rarely
/// restates what it is not changing and should not lose it for that.
/// </summary>
[McpServerToolType]
public sealed class TaskMcpTools(RoadmapDbContext db)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    private async Task<string> Echo(Guid roadmapId, Guid taskId) =>
        J(TaskLogic.ToDto((await TaskLogic.LoadOneAsync(db, roadmapId, taskId))!));

    private async Task<string> EchoProject(Guid roadmapId, Guid projectId) =>
        J((await TaskLogic.LoadProjectsAsync(db, roadmapId)).First(p => p.Id == projectId));

    // ===== Tasks =====

    [McpServerTool(Name = "list_tasks"), Description(
        "List a roadmap's single (one-off) tasks: open ones first, then by priority High → Medium → Low, then oldest " +
        "first. Each carries its Project (id, name, colour — or null), DependsOn (the tasks it waits on, with whether " +
        "each is done), Dependents (the tasks waiting on it) and IsBlocked — true while any prerequisite is unfinished; " +
        "a blocked task is left out of the day's schedule. Filter by project_id and/or completed.")]
    public async Task<string> ListTasks(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Only tasks of this project (see list_task_projects)")] Guid? project_id = null,
        [Description("true = only done tasks, false = only open ones; omit for both")] bool? completed = null)
    {
        var all = await TaskLogic.LoadAsync(db, roadmap_id, project_id, completed);
        return J(all.Select(TaskLogic.ToDto));
    }

    [McpServerTool(Name = "create_task"), Description(
        "Create a single (one-off) task. Earns floor(estimated_hours * 2) points when completed within a sprint. " +
        "File it under a project with project_id, or with project (a name — an unknown name creates the project, " +
        "a known one is matched case-insensitively). Name the tasks it must wait for in depends_on_ids: the task " +
        "is blocked, and kept out of the schedule, until every one of them is done. A dependency on itself, on a " +
        "task of another roadmap, or one that would make a cycle is refused and the result says why.")]
    public async Task<string> CreateTask(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Task title")] string title,
        [Description("Priority: High, Medium, or Low")] string priority,
        [Description("Estimated hours to complete")] double estimated_hours,
        [Description("Date to start showing this task (YYYY-MM-DD)")] string start_date,
        [Description("Optional due date (YYYY-MM-DD)")] string? due_date = null,
        [Description("Project UUID to file the task under")] Guid? project_id = null,
        [Description("Project name instead of an id; created if it does not exist")] string? project = null,
        [Description("UUIDs of the tasks this one depends on (must be done before it)")] Guid[]? depends_on_ids = null,
        [Description("Weekdays it may be scheduled on, 0 = Sunday … 6 = Saturday, e.g. [1,2,3,4,5]; omit for any day")] string? weekdays = null)
    {
        if (string.IsNullOrWhiteSpace(title)) return J(new { error = "title is required" });
        if (!DateOnly.TryParse(start_date, out var sd)) return J(new { error = "Invalid start_date." });
        DateOnly? dd = null;
        if (!string.IsNullOrWhiteSpace(due_date))
        {
            if (!DateOnly.TryParse(due_date, out var parsed)) return J(new { error = "Invalid due_date." });
            dd = parsed;
        }
        if (!TaskLogic.TryParsePriority(priority, out var pri)) return J(new { error = TaskLogic.PriorityHelp });
        if (!TaskLogic.TryParseWeekdays(weekdays, out var wd)) return J(new { error = TaskLogic.WeekdaysHelp });
        if (!(estimated_hours > 0)) return J(new { error = "estimated_hours must be positive" });
        var proj = await TaskLogic.ResolveProjectAsync(db, roadmap_id, project_id, project);
        if (proj.Error is not null) return J(new { error = proj.Error });

        var t = new SingleTask { Id = Guid.NewGuid(), RoadmapId = roadmap_id, Title = title.Trim(), Priority = pri,
            EstimatedHours = estimated_hours, Weekdays = wd, StartDate = sd, DueDate = dd, ProjectId = proj.Project?.Id };
        db.SingleTasks.Add(t);
        var depError = await TaskLogic.SetDependenciesAsync(db, roadmap_id, t.Id, depends_on_ids ?? []);
        if (depError is not null) return J(new { error = depError });
        await db.SaveChangesAsync();
        return await Echo(roadmap_id, t.Id);
    }

    [McpServerTool(Name = "update_task"), Description(
        "Change a task's fields. Only the arguments given change: null keeps the stored value; an empty string clears " +
        "due_date, weekdays or the project. project_id, or project (a name — unknown creates it), moves the task to a " +
        "project. Prerequisites are changed with set_task_dependencies / add_task_dependency / remove_task_dependency.")]
    public async Task<string> UpdateTask(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Task UUID")] Guid task_id,
        [Description("New title")] string? title = null,
        [Description("High, Medium or Low")] string? priority = null,
        [Description("Estimated hours")] double? estimated_hours = null,
        [Description("YYYY-MM-DD")] string? start_date = null,
        [Description("YYYY-MM-DD, or '' to clear")] string? due_date = null,
        [Description("e.g. [1,2,3,4,5], or '' for any day")] string? weekdays = null,
        [Description("Project UUID to move the task to")] Guid? project_id = null,
        [Description("Project name to move the task to (created if new), or '' to take it out of its project")] string? project = null)
    {
        var t = await db.SingleTasks.FirstOrDefaultAsync(x => x.Id == task_id && x.RoadmapId == roadmap_id);
        if (t is null) return J(new { error = "Task not found" });

        if (title is not null)
        {
            if (string.IsNullOrWhiteSpace(title)) return J(new { error = "title cannot be empty" });
            t.Title = title.Trim();
        }
        if (priority is not null)
        {
            if (!TaskLogic.TryParsePriority(priority, out var pri)) return J(new { error = TaskLogic.PriorityHelp });
            t.Priority = pri;
        }
        if (estimated_hours is double h)
        {
            if (!(h > 0)) return J(new { error = "estimated_hours must be positive" });
            t.EstimatedHours = h;
        }
        if (start_date is not null)
        {
            if (!DateOnly.TryParse(start_date, out var sd)) return J(new { error = "Invalid start_date." });
            t.StartDate = sd;
        }
        if (due_date is not null)
        {
            if (due_date.Trim() == "") t.DueDate = null;
            else if (DateOnly.TryParse(due_date, out var dd)) t.DueDate = dd;
            else return J(new { error = "Invalid due_date." });
        }
        if (weekdays is not null)
        {
            if (!TaskLogic.TryParseWeekdays(weekdays, out var wd)) return J(new { error = TaskLogic.WeekdaysHelp });
            t.Weekdays = wd;
        }
        if (project_id is not null || project is not null)
        {
            if (project_id is null && project!.Trim() == "") t.ProjectId = null;
            else
            {
                var proj = await TaskLogic.ResolveProjectAsync(db, roadmap_id, project_id, project);
                if (proj.Error is not null) return J(new { error = proj.Error });
                t.ProjectId = proj.Project?.Id;
            }
        }
        await db.SaveChangesAsync();
        return await Echo(roadmap_id, task_id);
    }

    [McpServerTool(Name = "set_task_dependencies"), Description(
        "Replace the whole set of tasks this task waits on ([] clears it). Every id must be a task of the same " +
        "roadmap; the task itself, or anything that already waits on it (directly or through others), is refused " +
        "because it would make a cycle — the error names the loop. Returns the task with its new DependsOn and IsBlocked.")]
    public async Task<string> SetTaskDependencies(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Task UUID")] Guid task_id,
        [Description("UUIDs of the tasks it must wait for; [] for none")] Guid[] depends_on_ids)
    {
        if (!await db.SingleTasks.AnyAsync(x => x.Id == task_id && x.RoadmapId == roadmap_id)) return J(new { error = "Task not found" });
        var error = await TaskLogic.SetDependenciesAsync(db, roadmap_id, task_id, depends_on_ids);
        if (error is not null) return J(new { error });
        await db.SaveChangesAsync();
        return await Echo(roadmap_id, task_id);
    }

    [McpServerTool(Name = "add_task_dependency"), Description(
        "Make task_id wait on depends_on_task_id (both in the same roadmap). Refused when it would make a cycle. " +
        "Adding one that is already there is fine. Returns the task.")]
    public async Task<string> AddTaskDependency(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("The task that waits")] Guid task_id,
        [Description("The task it waits on")] Guid depends_on_task_id)
    {
        if (!await db.SingleTasks.AnyAsync(x => x.Id == task_id && x.RoadmapId == roadmap_id)) return J(new { error = "Task not found" });
        var current = await db.TaskDependencies.AsNoTracking().Where(d => d.TaskId == task_id).Select(d => d.DependsOnTaskId).ToListAsync();
        if (!current.Contains(depends_on_task_id)) current.Add(depends_on_task_id);
        var error = await TaskLogic.SetDependenciesAsync(db, roadmap_id, task_id, current);
        if (error is not null) return J(new { error });
        await db.SaveChangesAsync();
        return await Echo(roadmap_id, task_id);
    }

    [McpServerTool(Name = "remove_task_dependency"), Description(
        "Stop task_id waiting on depends_on_task_id. Removing one that is not there is fine. Returns the task.")]
    public async Task<string> RemoveTaskDependency(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("The task that waits")] Guid task_id,
        [Description("The task it no longer waits on")] Guid depends_on_task_id)
    {
        if (!await db.SingleTasks.AnyAsync(x => x.Id == task_id && x.RoadmapId == roadmap_id)) return J(new { error = "Task not found" });
        var edge = await db.TaskDependencies.FirstOrDefaultAsync(d => d.TaskId == task_id && d.DependsOnTaskId == depends_on_task_id);
        if (edge is not null) { db.TaskDependencies.Remove(edge); await db.SaveChangesAsync(); }
        return await Echo(roadmap_id, task_id);
    }

    [McpServerTool(Name = "complete_task"), Description(
        "Mark a task as completed on a specific date, crediting points to that date's sprint performance. Completing " +
        "a blocked task is allowed (a dependency is advice about order, not a lock); the result lists the unfinished " +
        "prerequisites it was completed ahead of, so you can say so.")]
    public async Task<string> CompleteTask(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Task UUID")] Guid task_id,
        [Description("Completion date (YYYY-MM-DD)")] string date)
    {
        var t = await db.SingleTasks.Include(x => x.DependsOn).ThenInclude(d => d.DependsOnTask)
            .FirstOrDefaultAsync(x => x.Id == task_id && x.RoadmapId == roadmap_id);
        if (t is null) return J(new { error = "Task not found" });
        if (!DateOnly.TryParse(date, out var cd)) return J(new { error = "Invalid date." });
        var ahead = TaskLogic.OpenPrerequisites(t);
        t.IsCompleted = true; t.CompletedDate = cd;
        await db.SaveChangesAsync();
        return J(new { success = true, points = TaskLogic.Points(t), completed_ahead_of = ahead });
    }

    [McpServerTool(Name = "delete_task"), Description(
        "Delete a single task. Its dependency edges go with it, so anything that waited on it stops waiting; the " +
        "result says how many tasks that unblocked.")]
    public async Task<string> DeleteTask(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Task UUID")] Guid task_id)
    {
        var t = await db.SingleTasks.FirstOrDefaultAsync(x => x.Id == task_id && x.RoadmapId == roadmap_id);
        if (t is null) return J(new { error = "Task not found" });
        var dependents = await db.TaskDependencies.AsNoTracking().CountAsync(d => d.DependsOnTaskId == task_id);
        db.SingleTasks.Remove(t);
        await db.SaveChangesAsync();
        return J(new { success = true, dependents_released = dependents });
    }

    // ===== Projects =====

    [McpServerTool(Name = "list_task_projects"), Description(
        "List a roadmap's task projects, by name, each with its colour and how many of its tasks are open, done " +
        "and blocked. A project is a label that groups tasks; a task has at most one.")]
    public async Task<string> ListTaskProjects([Description("Roadmap UUID")] Guid roadmap_id) =>
        J(await TaskLogic.LoadProjectsAsync(db, roadmap_id));

    [McpServerTool(Name = "create_task_project"), Description(
        "Create a task project. Names are unique per roadmap, case-insensitively. " + TaskLogic.ColorHelp + ".")]
    public async Task<string> CreateTaskProject(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Project name")] string name,
        [Description("blue | orange | aqua | yellow | magenta | green | violet | red; omit to have the least-used one picked")] string? color = null)
    {
        var clean = TaskLogic.CleanProjectName(name);
        if (clean is null) return J(new { error = "name is required" });
        if (!TaskLogic.TryParseColor(color, out var c)) return J(new { error = TaskLogic.ColorHelp });
        var existing = await TaskLogic.FindProjectByNameAsync(db, roadmap_id, clean);
        if (existing is not null) return J(new { error = $"a project named \"{existing.Name}\" already exists", project_id = existing.Id });
        var p = await TaskLogic.NewProjectAsync(db, roadmap_id, clean, c);
        await db.SaveChangesAsync();
        return await EchoProject(roadmap_id, p.Id);
    }

    [McpServerTool(Name = "update_task_project"), Description(
        "Rename or recolour a task project. Null keeps a field. " + TaskLogic.ColorHelp + ".")]
    public async Task<string> UpdateTaskProject(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Project UUID")] Guid project_id,
        [Description("New name")] string? name = null,
        [Description("blue | orange | aqua | yellow | magenta | green | violet | red")] string? color = null)
    {
        var p = await db.TaskProjects.FirstOrDefaultAsync(x => x.Id == project_id && x.RoadmapId == roadmap_id);
        if (p is null) return J(new { error = "Project not found" });
        if (name is not null)
        {
            var clean = TaskLogic.CleanProjectName(name);
            if (clean is null) return J(new { error = "name cannot be empty" });
            var clash = await TaskLogic.FindProjectByNameAsync(db, roadmap_id, clean);
            if (clash is not null && clash.Id != p.Id) return J(new { error = $"a project named \"{clash.Name}\" already exists" });
            p.Name = clean;
        }
        if (color is not null)
        {
            if (!TaskLogic.TryParseColor(color, out var c) || c is null) return J(new { error = TaskLogic.ColorHelp });
            p.Color = c;
        }
        await db.SaveChangesAsync();
        return await EchoProject(roadmap_id, project_id);
    }

    [McpServerTool(Name = "delete_task_project"), Description(
        "Delete a task project. Its tasks are kept and become unassigned; the result says how many.")]
    public async Task<string> DeleteTaskProject(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Project UUID")] Guid project_id)
    {
        var p = await db.TaskProjects.FirstOrDefaultAsync(x => x.Id == project_id && x.RoadmapId == roadmap_id);
        if (p is null) return J(new { error = "Project not found" });
        var tasks = await db.SingleTasks.AsNoTracking().CountAsync(t => t.ProjectId == project_id);
        db.TaskProjects.Remove(p);
        await db.SaveChangesAsync();
        return J(new { success = true, tasks_unassigned = tasks });
    }
}
