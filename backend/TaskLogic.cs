using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// Everything about a single task, its project and its dependencies that the REST endpoints and
/// the MCP tools must agree on: what a project name resolves to, which colours a project may have,
/// when a dependency is refused (itself, another roadmap, a cycle), what "blocked" means, how the
/// list is ordered and how a task is serialised. Static and shared for the same reason as
/// <c>ExperienceLogic</c> — two surfaces write the same rows, and the only way they cannot drift
/// is to not each have a copy.
/// </summary>
public static partial class TaskLogic
{
    /// <summary>
    /// The eight hues of the app's validated categorical palette (the icon palette in styles.css).
    /// A project stores the key, not a hex: the tab maps it to a theme-aware token, so a project
    /// keeps a legible colour on the dark surface without a second column.
    /// </summary>
    public static readonly string[] Palette = ["blue", "orange", "aqua", "yellow", "magenta", "green", "violet", "red"];
    public const string ColorHelp = "color must be one of blue, orange, aqua, yellow, magenta, green, violet, red — or blank to have one picked";
    public const string PriorityHelp = "priority must be High, Medium or Low";
    public const string WeekdaysHelp = "weekdays must be a JSON array of weekday numbers 0-6 (0 = Sunday), e.g. [1,2,3,4,5], or blank for any day";
    public const int MaxProjectNameLength = 120;

    public static double Points(SingleTask t) => Math.Floor(t.EstimatedHours * 2);

    /// <summary>Not done, and waiting on at least one prerequisite that is not done either. Needs <c>DependsOn.DependsOnTask</c> loaded.</summary>
    public static bool IsBlocked(SingleTask t) => !t.IsCompleted && t.DependsOn.Any(d => !d.DependsOnTask.IsCompleted);

    /// <summary>The prerequisites still in the way, titles for a message. Empty when not blocked.</summary>
    public static List<TaskRefDto> OpenPrerequisites(SingleTask t) => t.IsCompleted ? [] : t.DependsOn
        .Where(d => !d.DependsOnTask.IsCompleted)
        .OrderBy(d => d.DependsOnTask.Title)
        .Select(d => new TaskRefDto(d.DependsOnTaskId, d.DependsOnTask.Title, false)).ToList();

    // ── reading input ──

    public static bool TryParsePriority(string? raw, out TaskPriority priority)
    {
        priority = TaskPriority.Medium;
        return !string.IsNullOrWhiteSpace(raw)
            && Enum.TryParse(raw.Trim(), ignoreCase: true, out priority) && Enum.IsDefined(priority);
    }

    /// <summary>
    /// Weekdays as stored: a JSON array of distinct ints 0-6 in ascending order, or null for "any
    /// day". Accepts the same JSON, or a bare comma list ("1,2,3") for an agent typing by hand.
    /// </summary>
    public static bool TryParseWeekdays(string? raw, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var text = raw.Trim();
        int[]? days = null;
        try
        {
            if (text.StartsWith('[')) days = JsonSerializer.Deserialize<int[]>(text);
            else days = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
        }
        catch (Exception e) when (e is JsonException or FormatException or OverflowException) { return false; }
        if (days is null) return false;
        if (days.Any(d => d < 0 || d > 6)) return false;
        var clean = days.Distinct().Order().ToArray();
        normalized = clean.Length == 0 ? null : JsonSerializer.Serialize(clean);
        return true;
    }

    // ── projects ──

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>The name as it will be stored: trimmed, inner whitespace collapsed, capped. Null for blank.</summary>
    public static string? CleanProjectName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var clean = Spaces().Replace(raw.Trim(), " ");
        if (clean.Length > MaxProjectNameLength) clean = clean[..MaxProjectNameLength].TrimEnd();
        return clean;
    }

    /// <summary>A palette key, lower-cased; null (meaning "pick one") for blank. False for anything else.</summary>
    public static bool TryParseColor(string? raw, out string? color)
    {
        color = null;
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var key = raw.Trim().ToLowerInvariant();
        if (!Palette.Contains(key)) return false;
        color = key;
        return true;
    }

    /// <summary>
    /// The hue the fewest of this roadmap's projects already wear, ties in palette order — so the
    /// first eight projects all differ and a ninth repeats the least-used one, without asking.
    /// </summary>
    public static async Task<string> PickColorAsync(RoadmapDbContext db, Guid roadmapId)
    {
        var used = await db.TaskProjects.AsNoTracking().Where(p => p.RoadmapId == roadmapId).Select(p => p.Color).ToListAsync();
        var counts = used.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        return Palette.OrderBy(c => counts.GetValueOrDefault(c, 0)).First(); // OrderBy is stable: palette order on ties
    }

    /// <summary>Case-insensitive lookup — "launch" finds "Launch", so a second spelling is never created.</summary>
    public static Task<TaskProject?> FindProjectByNameAsync(RoadmapDbContext db, Guid roadmapId, string name)
    {
        var lower = name.ToLowerInvariant();
        return db.TaskProjects.FirstOrDefaultAsync(p => p.RoadmapId == roadmapId && p.Name.ToLower() == lower);
    }

    /// <summary>A new project, added to the context but not yet saved. Blank colour → picked.</summary>
    public static async Task<TaskProject> NewProjectAsync(RoadmapDbContext db, Guid roadmapId, string name, string? color)
    {
        var p = new TaskProject { Id = Guid.NewGuid(), RoadmapId = roadmapId, Name = name, Color = color ?? await PickColorAsync(db, roadmapId) };
        db.TaskProjects.Add(p);
        return p;
    }

    public sealed record ProjectResolution(TaskProject? Project, string? Error);

    /// <summary>
    /// The project a task should be filed under: by id (must exist in this roadmap) or, failing an
    /// id, by name — an existing one case-insensitively, else a new one (added, not yet saved). Both
    /// blank means no project. An assistant says "put it in Launch"; it should not have to look the
    /// id up first, and a typo in case should not make a twin.
    /// </summary>
    public static async Task<ProjectResolution> ResolveProjectAsync(RoadmapDbContext db, Guid roadmapId, Guid? id, string? name)
    {
        if (id is Guid pid)
        {
            var p = await db.TaskProjects.FirstOrDefaultAsync(x => x.Id == pid && x.RoadmapId == roadmapId);
            return p is null ? new(null, "project not found in this roadmap") : new(p, null);
        }
        var clean = CleanProjectName(name);
        if (clean is null) return new(null, null);
        var existing = await FindProjectByNameAsync(db, roadmapId, clean);
        return new(existing ?? await NewProjectAsync(db, roadmapId, clean, null), null);
    }

    public static async Task<List<TaskProjectDto>> LoadProjectsAsync(RoadmapDbContext db, Guid roadmapId)
    {
        var projects = await db.TaskProjects.AsNoTracking().Where(p => p.RoadmapId == roadmapId).OrderBy(p => p.Name).ToListAsync();
        var tasks = await db.SingleTasks.AsNoTracking()
            .Where(t => t.RoadmapId == roadmapId && t.ProjectId != null)
            .Include(t => t.DependsOn).ThenInclude(d => d.DependsOnTask)
            .ToListAsync();
        var byProject = tasks.GroupBy(t => t.ProjectId!.Value).ToDictionary(g => g.Key, g => (IReadOnlyCollection<SingleTask>)g.ToList());
        return projects.Select(p => ToDto(p, byProject.GetValueOrDefault(p.Id) ?? [])).ToList();
    }

    public static TaskProjectDto ToDto(TaskProject p, IReadOnlyCollection<SingleTask> tasks) => new(p.Id, p.Name, p.Color,
        tasks.Count(t => !t.IsCompleted), tasks.Count(t => t.IsCompleted), tasks.Count(IsBlocked), p.CreatedAt.ToString("o"));

    public static TaskProjectRefDto? Ref(TaskProject? p) => p is null ? null : new(p.Id, p.Name, p.Color);

    // ── dependencies ──

    /// <summary>
    /// Why <paramref name="dependsOnIds"/> cannot be this task's prerequisites, or null when they
    /// can: a task cannot wait on itself, on a task of another roadmap, or on anything that already
    /// (transitively) waits on it — that is a cycle, and the message spells the loop out by title.
    /// The check is made against the graph as it would be after the write, so replacing a set is
    /// judged as a whole rather than edge by edge.
    /// </summary>
    public static async Task<string?> ValidateDependenciesAsync(RoadmapDbContext db, Guid roadmapId, Guid taskId, IReadOnlyCollection<Guid> dependsOnIds)
    {
        if (dependsOnIds.Count == 0) return null;
        if (dependsOnIds.Contains(taskId)) return "a task cannot depend on itself";
        var ids = dependsOnIds.Distinct().ToList();

        var titles = await db.SingleTasks.AsNoTracking().Where(t => t.RoadmapId == roadmapId)
            .Select(t => new { t.Id, t.Title }).ToDictionaryAsync(t => t.Id, t => t.Title);
        var missing = ids.Where(i => !titles.ContainsKey(i)).ToList();
        if (missing.Count > 0)
            return $"unknown task id{(missing.Count > 1 ? "s" : "")} in depends_on: {string.Join(", ", missing)} (not in this roadmap)";

        // Edges as they would stand after the write: every edge in the roadmap except this task's
        // own (those are being replaced), plus the proposed ones.
        var edges = await db.TaskDependencies.AsNoTracking()
            .Where(d => d.Task.RoadmapId == roadmapId && d.TaskId != taskId)
            .Select(d => new { d.TaskId, d.DependsOnTaskId }).ToListAsync();
        var graph = edges.GroupBy(e => e.TaskId).ToDictionary(g => g.Key, g => g.Select(e => e.DependsOnTaskId).ToList());
        graph[taskId] = ids;

        // A cycle exists iff the task is reachable from one of its own prerequisites.
        var path = FindPath(graph, from: ids, to: taskId);
        if (path is null) return null;
        var self = titles.GetValueOrDefault(taskId, "this task");
        var loop = string.Join(" → ", path.Select(id => titles.GetValueOrDefault(id, id.ToString())));
        return $"that would make a cycle: {self} → {loop}";
    }

    /// <summary>Breadth-first over "depends on" edges from any of <paramref name="from"/>; the path to <paramref name="to"/>, or null.</summary>
    private static List<Guid>? FindPath(Dictionary<Guid, List<Guid>> graph, IEnumerable<Guid> from, Guid to)
    {
        var parent = new Dictionary<Guid, Guid?>();
        var queue = new Queue<Guid>();
        foreach (var s in from) if (parent.TryAdd(s, null)) queue.Enqueue(s);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == to)
            {
                var path = new List<Guid>();
                for (Guid? c = cur; c is Guid g; c = parent[g]) path.Add(g);
                path.Reverse();
                return path;
            }
            if (!graph.TryGetValue(cur, out var next)) continue;
            foreach (var n in next) if (parent.TryAdd(n, cur)) queue.Enqueue(n);
        }
        return null;
    }

    /// <summary>
    /// Make <paramref name="dependsOnIds"/> the whole set of this task's prerequisites — validated
    /// first, then rows added and removed on the context (not saved). An empty set clears them.
    /// Works for a task that is itself not yet saved: EF orders the inserts.
    /// </summary>
    public static async Task<string?> SetDependenciesAsync(RoadmapDbContext db, Guid roadmapId, Guid taskId, IReadOnlyCollection<Guid> dependsOnIds)
    {
        var error = await ValidateDependenciesAsync(db, roadmapId, taskId, dependsOnIds);
        if (error is not null) return error;
        var wanted = dependsOnIds.Distinct().ToHashSet();
        var current = await db.TaskDependencies.Where(d => d.TaskId == taskId).ToListAsync();
        db.TaskDependencies.RemoveRange(current.Where(d => !wanted.Contains(d.DependsOnTaskId)));
        var have = current.Select(d => d.DependsOnTaskId).ToHashSet();
        foreach (var id in wanted.Where(id => !have.Contains(id)))
            db.TaskDependencies.Add(new TaskDependency { TaskId = taskId, DependsOnTaskId = id });
        return null;
    }

    // ── reading tasks ──

    /// <summary>A task with everything its DTO needs: project, prerequisites (with their state), dependents.</summary>
    public static IQueryable<SingleTask> Query(RoadmapDbContext db, Guid roadmapId) => db.SingleTasks.AsNoTracking()
        .Where(t => t.RoadmapId == roadmapId)
        .Include(t => t.Project)
        .Include(t => t.DependsOn).ThenInclude(d => d.DependsOnTask)
        .Include(t => t.Dependents).ThenInclude(d => d.Task);

    /// <summary>
    /// Open tasks first, then by priority High → Medium → Low, then oldest first. Ordered in memory:
    /// the column holds the enum's name, and ordering on it in SQL would put Low before Medium.
    /// </summary>
    public static async Task<List<SingleTask>> LoadAsync(RoadmapDbContext db, Guid roadmapId, Guid? projectId = null, bool? completed = null)
    {
        var q = Query(db, roadmapId);
        if (projectId is Guid pid) q = q.Where(t => t.ProjectId == pid);
        if (completed is bool c) q = q.Where(t => t.IsCompleted == c);
        var rows = await q.ToListAsync();
        return rows.OrderBy(t => t.IsCompleted).ThenBy(t => t.Priority).ThenBy(t => t.CreatedAt).ToList();
    }

    public static Task<SingleTask?> LoadOneAsync(RoadmapDbContext db, Guid roadmapId, Guid taskId) =>
        Query(db, roadmapId).FirstOrDefaultAsync(t => t.Id == taskId);

    public static SingleTaskDto ToDto(SingleTask t) => new(t.Id, t.Title, t.Priority.ToString(),
        t.EstimatedHours, t.Weekdays, t.StartDate.ToString("yyyy-MM-dd"),
        t.DueDate?.ToString("yyyy-MM-dd"), t.DelayedUntil?.ToString("yyyy-MM-dd"),
        t.IsCompleted, t.CompletedDate?.ToString("yyyy-MM-dd"), Points(t),
        Ref(t.Project),
        t.DependsOn.OrderBy(d => d.DependsOnTask.Title)
            .Select(d => new TaskRefDto(d.DependsOnTaskId, d.DependsOnTask.Title, d.DependsOnTask.IsCompleted)).ToList(),
        t.Dependents.OrderBy(d => d.Task.Title)
            .Select(d => new TaskRefDto(d.TaskId, d.Task.Title, d.Task.IsCompleted)).ToList(),
        IsBlocked(t));
}
