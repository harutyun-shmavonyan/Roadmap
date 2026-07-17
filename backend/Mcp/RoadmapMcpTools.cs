using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Mcp;

[McpServerToolType]
public sealed class RoadmapMcpTools(RoadmapDbContext db)
{
    private static string J(object? v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true });

    // ===== Roadmaps =====

    [McpServerTool(Name = "list_roadmaps"), Description("List all roadmaps with their IDs, names, and descriptions.")]
    public async Task<string> ListRoadmaps()
    {
        var result = await db.Roadmaps.OrderBy(r => r.CreatedAt)
            .Select(r => new RoadmapSummaryDto(r.Id, r.Name, r.Description, r.CreatedAt))
            .ToListAsync();
        return J(result);
    }

    [McpServerTool(Name = "get_roadmap_tree"), Description("Get the full goal tree for a roadmap — all categories and action items with hierarchy, status, and metrics.")]
    public async Task<string> GetRoadmapTree([Description("Roadmap UUID")] Guid roadmap_id)
    {
        var rm = await db.Roadmaps.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roadmap_id);
        if (rm is null) return J(new { error = "Roadmap not found" });
        var nodes = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmap_id).OrderBy(n => n.SortOrder).ToListAsync();
        var links = await db.NodeCategoryLinks.AsNoTracking().Where(l => nodes.Select(n => n.Id).Contains(l.NodeId)).ToListAsync();
        var lk = nodes.ToDictionary(n => n.Id);
        return J(new RoadmapTreeDto(rm.Id, rm.Name, rm.Description, BuildTree(nodes, links, lk, null)));
    }

    [McpServerTool(Name = "get_actionable_items"), Description("Get action items for a roadmap. Optionally filter by status: NotStarted, Active, Paused, Stopped, Completed.")]
    public async Task<string> GetActionableItems(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Filter by status (optional)")] string? status = null)
    {
        var nodes = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmap_id).OrderBy(n => n.SortOrder).ToListAsync();
        var acts = nodes.Where(n => n.IsActionable).AsEnumerable();
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ActionItemStatus>(status, true, out var ps))
            acts = acts.Where(n => n.Status == ps);
        var lk = nodes.ToDictionary(n => n.Id);
        var ids = acts.Select(n => n.Id).ToList();
        var logs = await db.WorkLogs.Where(w => w.RoadmapId == roadmap_id && ids.Contains(w.NodeId))
            .GroupBy(w => w.NodeId).Select(g => new { g.Key, Total = g.Sum(w => w.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
        var result = acts.Select(n => new ActionableItemDto(n.Id, n.Title, BuildPath(n, lk), n.Status.ToString(),
            n.Unit, n.TotalSize, n.UnitsPerHour, n.PointsPerUnit, logs.GetValueOrDefault(n.Id, 0), n.ScheduleTemplate)).ToList();
        return J(result);
    }

    [McpServerTool(Name = "create_roadmap"), Description("Create a new roadmap.")]
    public async Task<string> CreateRoadmap(
        [Description("Roadmap name")] string name,
        [Description("Optional description")] string? description = null)
    {
        var rm = new RoadmapDefinition { Id = Guid.NewGuid(), Name = name, Description = description };
        db.Roadmaps.Add(rm);
        await db.SaveChangesAsync();
        return J(new RoadmapSummaryDto(rm.Id, rm.Name, rm.Description, rm.CreatedAt));
    }

    [McpServerTool(Name = "delete_roadmap"), Description("Permanently delete a roadmap and all its nodes, sprints, and logs.")]
    public async Task<string> DeleteRoadmap([Description("Roadmap UUID")] Guid roadmap_id)
    {
        var rm = await db.Roadmaps.FindAsync(roadmap_id);
        if (rm is null) return J(new { error = "Roadmap not found" });
        db.Roadmaps.Remove(rm);
        await db.SaveChangesAsync();
        return J(new { success = true });
    }

    // ===== Nodes =====

    [McpServerTool(Name = "create_node"), Description("Add a node to the roadmap tree. is_actionable=false for categories (branches), true for action items (trackable leaves).")]
    public async Task<string> CreateNode(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Node title")] string title,
        [Description("true = action item (leaf), false = category (branch)")] bool is_actionable,
        [Description("Parent node UUID (omit for root level)")] Guid? parent_id = null,
        [Description("Unit of work, e.g. 'pages', 'hours'")] string? unit = null,
        [Description("Total work units to complete")] double? total_size = null,
        [Description("Throughput rate (units per hour)")] double? units_per_hour = null,
        [Description("Points earned per unit of work")] double? points_per_unit = null,
        [Description("Sort position among siblings")] int sort_order = 0)
    {
        if (!await db.Roadmaps.AnyAsync(r => r.Id == roadmap_id))
            return J(new { error = "Roadmap not found" });
        if (parent_id.HasValue && !await db.Nodes.AnyAsync(n => n.Id == parent_id.Value && n.RoadmapId == roadmap_id))
            return J(new { error = "Parent node not found" });
        var node = new RoadmapNode
        {
            Id = Guid.NewGuid(), RoadmapId = roadmap_id, ParentId = parent_id,
            Title = title, IsActionable = is_actionable, SortOrder = sort_order,
            Unit = unit, TotalSize = total_size, UnitsPerHour = units_per_hour, PointsPerUnit = points_per_unit
        };
        db.Nodes.Add(node);
        await db.SaveChangesAsync();
        return J(new NodeDto(node.Id, node.ParentId, node.Title, node.IsActionable, node.Status.ToString(),
            node.Unit, node.TotalSize, node.UnitsPerHour, node.PointsPerUnit, node.ScheduleTemplate,
            node.SortOrder, node.ScheduleBlockId, node.BlockSortOrder, [], []));
    }

    [McpServerTool(Name = "update_node"), Description("Update an existing node's title, type, or metrics.")]
    public async Task<string> UpdateNode(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Node UUID")] Guid node_id,
        [Description("Node title")] string title,
        [Description("true = action item, false = category")] bool is_actionable,
        [Description("Unit of work")] string? unit = null,
        [Description("Total work units")] double? total_size = null,
        [Description("Throughput rate")] double? units_per_hour = null,
        [Description("Points per unit")] double? points_per_unit = null,
        [Description("Sort position")] int sort_order = 0)
    {
        var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == node_id && n.RoadmapId == roadmap_id);
        if (node is null) return J(new { error = "Node not found" });
        node.Title = title; node.IsActionable = is_actionable; node.SortOrder = sort_order;
        node.Unit = unit; node.TotalSize = total_size; node.UnitsPerHour = units_per_hour; node.PointsPerUnit = points_per_unit;
        await db.SaveChangesAsync();
        return J(new { success = true });
    }

    [McpServerTool(Name = "change_node_status"), Description("Change the status of an action item. Valid values: NotStarted, Active, Paused, Stopped, Completed.")]
    public async Task<string> ChangeNodeStatus(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Node UUID (must be an action item)")] Guid node_id,
        [Description("New status: NotStarted, Active, Paused, Stopped, Completed")] string status)
    {
        var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == node_id && n.RoadmapId == roadmap_id);
        if (node is null) return J(new { error = "Node not found" });
        if (!Enum.TryParse<ActionItemStatus>(status, true, out var st)) return J(new { error = "Invalid status" });
        var old = node.Status;
        if (old == st) return J(new { success = true, message = "Status unchanged" });
        node.Status = st;
        db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = roadmap_id, NodeId = node_id, OldStatus = old, NewStatus = st, Trigger = "mcp" });
        await db.SaveChangesAsync();
        return J(new { success = true, oldStatus = old.ToString(), newStatus = st.ToString() });
    }

    [McpServerTool(Name = "delete_node"), Description("Delete a node and all its descendants from the roadmap tree.")]
    public async Task<string> DeleteNode(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Node UUID")] Guid node_id)
    {
        var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == node_id && n.RoadmapId == roadmap_id);
        if (node is null) return J(new { error = "Node not found" });
        db.Nodes.Remove(node);
        await db.SaveChangesAsync();
        return J(new { success = true });
    }

    [McpServerTool(Name = "get_status_history"), Description("Get status change history for a roadmap or a specific node, ordered most recent first.")]
    public async Task<string> GetStatusHistory(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Node UUID (omit for whole-roadmap history)")] Guid? node_id = null,
        [Description("Max records to return (default 50)")] int limit = 50)
    {
        var q = db.StatusChanges.AsNoTracking().Include(s => s.Node)
            .Where(s => s.RoadmapId == roadmap_id);
        if (node_id.HasValue) q = q.Where(s => s.NodeId == node_id.Value);
        var changes = await q.OrderByDescending(s => s.ChangedAt).Take(limit).ToListAsync();
        return J(changes.Select(s => new StatusChangeDto(s.Id, s.NodeId, s.Node.Title,
            s.OldStatus.ToString(), s.NewStatus.ToString(), s.Trigger, s.ChangedAt.ToString("yyyy-MM-dd HH:mm:ss"))));
    }

    // ===== Sprints =====

    [McpServerTool(Name = "list_sprints"), Description("List all sprints for a roadmap, newest first.")]
    public async Task<string> ListSprints([Description("Roadmap UUID")] Guid roadmap_id)
    {
        var result = await db.Sprints.AsNoTracking().Where(s => s.RoadmapId == roadmap_id)
            .OrderByDescending(s => s.StartDate)
            .Select(s => new SprintDto(s.Id, s.Name, s.StartDate.ToString("yyyy-MM-dd"), s.EndDate.ToString("yyyy-MM-dd"), s.IsOpen, s.IsStarted, s.RelaxDays))
            .ToListAsync();
        return J(result);
    }

    [McpServerTool(Name = "create_sprint"), Description("Create a new sprint. Dates must not overlap with existing sprints.")]
    public async Task<string> CreateSprint(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Sprint name, e.g. 'Sprint 1 — May 2026'")] string name,
        [Description("Start date (YYYY-MM-DD)")] string start_date,
        [Description("End date (YYYY-MM-DD)")] string end_date)
    {
        if (!DateOnly.TryParse(start_date, out var s) || !DateOnly.TryParse(end_date, out var e))
            return J(new { error = "Invalid dates. Use YYYY-MM-DD format." });
        if (e <= s) return J(new { error = "End date must be after start date." });
        var overlap = await db.Sprints.AnyAsync(x => x.RoadmapId == roadmap_id && x.StartDate <= e && x.EndDate >= s);
        if (overlap) return J(new { error = "Sprint dates overlap with an existing sprint." });
        var sp = new Sprint { Id = Guid.NewGuid(), RoadmapId = roadmap_id, Name = name, StartDate = s, EndDate = e };
        db.Sprints.Add(sp);
        await db.SaveChangesAsync();
        return J(new SprintDto(sp.Id, sp.Name, sp.StartDate.ToString("yyyy-MM-dd"), sp.EndDate.ToString("yyyy-MM-dd"), sp.IsOpen, sp.IsStarted, sp.RelaxDays));
    }

    [McpServerTool(Name = "start_sprint"), Description("Start a sprint — snapshots the planned schedule for all active/scheduled nodes. Required before logging work.")]
    public async Task<string> StartSprint(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Sprint UUID")] Guid sprint_id)
    {
        var sprint = await db.Sprints.Include(s => s.PlanEntries).FirstOrDefaultAsync(s => s.Id == sprint_id && s.RoadmapId == roadmap_id);
        if (sprint is null) return J(new { error = "Sprint not found" });
        if (sprint.IsStarted) return J(new { error = "Sprint already started" });

        var allNodes = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmap_id).OrderBy(n => n.SortOrder).ToListAsync();
        var blocks = await db.ScheduleBlocks.AsNoTracking().Include(sb => sb.Items).Where(sb => sb.RoadmapId == roadmap_id).ToListAsync();
        var allTimeLogged = await db.WorkLogs.AsNoTracking().Where(w => w.RoadmapId == roadmap_id)
            .GroupBy(w => w.NodeId).Select(g => new { g.Key, Total = g.Sum(w => w.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);

        var dates = new List<DateOnly>();
        for (var d = sprint.StartDate; d <= sprint.EndDate; d = d.AddDays(1)) dates.Add(d);

        var relaxSet = new HashSet<string>();
        if (!string.IsNullOrEmpty(sprint.RelaxDays))
            try { relaxSet = JsonSerializer.Deserialize<HashSet<string>>(sprint.RelaxDays) ?? []; } catch { }

        var computed = ComputeSprintPlan(allNodes, blocks, dates, allTimeLogged, relaxSet);
        var entries = computed.Select(c => new SprintPlanEntry
        {
            Id = Guid.NewGuid(), SprintId = sprint.Id, NodeId = c.NodeId,
            Date = c.Date, StartMinute = c.StartMinute, DurationMinutes = c.DurationMinutes, PlannedUnits = c.PlannedUnits
        }).ToList();

        sprint.IsStarted = true;
        sprint.StartedAt = DateTime.UtcNow;
        db.SprintPlanEntries.AddRange(entries);
        await db.SaveChangesAsync();
        return J(new SprintDto(sprint.Id, sprint.Name, sprint.StartDate.ToString("yyyy-MM-dd"), sprint.EndDate.ToString("yyyy-MM-dd"), sprint.IsOpen, sprint.IsStarted, sprint.RelaxDays));
    }

    [McpServerTool(Name = "get_sprint_performance"), Description("Get performance summary for a sprint: planned vs earned points per item and daily point totals.")]
    public async Task<string> GetSprintPerformance(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Sprint UUID")] Guid sprint_id)
    {
        var sprint = await db.Sprints.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sprint_id && s.RoadmapId == roadmap_id);
        if (sprint is null) return J(new { error = "Sprint not found" });

        var planEntries = await db.SprintPlanEntries.AsNoTracking().Include(p => p.Node)
            .Where(p => p.SprintId == sprint_id).ToListAsync();
        var sprintLogs = await db.WorkLogs.AsNoTracking().Include(w => w.Node)
            .Where(w => w.SprintId == sprint_id).ToListAsync();

        var nodeIds = planEntries.Select(p => p.NodeId).Distinct().ToList();
        var items = nodeIds.Select(nid =>
        {
            var nodePlan = planEntries.Where(p => p.NodeId == nid).ToList();
            var node = nodePlan.First().Node;
            var plannedUnits = nodePlan.Sum(p => p.PlannedUnits);
            var doneUnits = sprintLogs.Where(w => w.NodeId == nid).Sum(w => w.Amount);
            var plannedPts = Math.Round(plannedUnits * (node.PointsPerUnit ?? 0), 1);
            var earnedPts = Math.Round(doneUnits * (node.PointsPerUnit ?? 0), 1);
            return new { nodeId = nid, title = node.Title, unit = node.Unit, plannedUnits = Math.Round(plannedUnits, 1), doneUnits = Math.Round(doneUnits, 1), plannedPoints = plannedPts, earnedPoints = earnedPts };
        }).ToList();

        var dates = new List<DateOnly>();
        for (var d = sprint.StartDate; d <= sprint.EndDate; d = d.AddDays(1)) dates.Add(d);
        var dailyMap = dates.ToDictionary(d => d, _ => 0.0);
        foreach (var log in sprintLogs)
        {
            if (dailyMap.ContainsKey(log.Date))
                dailyMap[log.Date] += log.Amount * (log.Node.PointsPerUnit ?? 0);
        }

        return J(new
        {
            sprint = new { sprint.Id, sprint.Name, startDate = sprint.StartDate.ToString("yyyy-MM-dd"), endDate = sprint.EndDate.ToString("yyyy-MM-dd") },
            totalPlannedPoints = Math.Round(items.Sum(i => i.plannedPoints), 1),
            totalEarnedPoints = Math.Round(items.Sum(i => i.earnedPoints), 1),
            items,
            dailyPoints = dailyMap.Select(kv => new { date = kv.Key.ToString("yyyy-MM-dd"), points = Math.Round(kv.Value, 1) }).OrderBy(x => x.date)
        });
    }

    // ===== Schedule =====

    [McpServerTool(Name = "get_daily_schedule"), Description("Get the work schedule for a specific date: scheduled blocks with planned units, start times, and progress.")]
    public async Task<string> GetDailySchedule(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Date (YYYY-MM-DD)")] string date)
    {
        if (!DateOnly.TryParse(date, out var pd)) return J(new { error = "Invalid date. Use YYYY-MM-DD." });
        var sprint = await db.Sprints.AsNoTracking().Where(s => s.RoadmapId == roadmap_id && s.StartDate <= pd && s.EndDate >= pd).FirstOrDefaultAsync();
        if (sprint is null) return J(new { blocks = Array.Empty<object>(), activeSprint = (object?)null, message = "No sprint covers this date." });

        var dow = (int)pd.DayOfWeek;
        var relaxSet = new HashSet<string>();
        if (!string.IsNullOrEmpty(sprint.RelaxDays))
            try { relaxSet = JsonSerializer.Deserialize<HashSet<string>>(sprint.RelaxDays) ?? []; } catch { }
        var isRelaxDay = relaxSet.Contains(pd.ToString("yyyy-MM-dd"));

        var allNodes = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmap_id).OrderBy(n => n.SortOrder).ToListAsync();
        var schedBlocks = await db.ScheduleBlocks.AsNoTracking().Include(sb => sb.Items).Where(sb => sb.RoadmapId == roadmap_id).ToListAsync();
        var lk = allNodes.ToDictionary(n => n.Id);
        var logTotals = await db.WorkLogs.AsNoTracking().Where(w => w.RoadmapId == roadmap_id)
            .GroupBy(w => w.NodeId).Select(g => new { g.Key, Total = g.Sum(w => w.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
        var workLogDatesList = await db.WorkLogs.AsNoTracking()
            .Where(w => w.RoadmapId == roadmap_id)
            .Select(w => new { w.NodeId, w.Date })
            .ToListAsync();
        var workLogDates = workLogDatesList
            .GroupBy(w => w.NodeId)
            .ToDictionary(g => g.Key, g => new HashSet<DateOnly>(g.Select(w => w.Date)));
        var today = DateOnly.FromDateTime(DateTime.Today);

        var blocks = new List<object>();
        var scheduledIds = new HashSet<Guid>();

        if (!isRelaxDay)
        {
            foreach (var sblock in schedBlocks)
            {
                var tmpl = ParseTemplate(sblock.ScheduleTemplate);
                if (tmpl is null || !tmpl.Days.Contains(dow)) continue;
                var queue = sblock.Items
                    .Where(n => n.IsActionable && (n.Status == ActionItemStatus.Active || n.Status == ActionItemStatus.NotStarted))
                    .OrderBy(n => n.BlockSortOrder).ToList();
                if (queue.Count == 0) continue;
                var projected = ProjectBlockQueueToDate(queue, tmpl, pd, sprint.StartDate, workLogDates, today);
                if (projected is null) continue;
                var dur = tmpl.GetDurationMinutes(dow);
                var planned = projected.UnitsPerHour.HasValue ? (dur / 60.0) * projected.UnitsPerHour.Value : 0;
                var logged = logTotals.GetValueOrDefault(projected.Id, 0);
                blocks.Add(new { nodeId = projected.Id, title = projected.Title, path = BuildPath(projected, lk), unit = projected.Unit, plannedUnits = Math.Round(planned, 1), startMinute = tmpl.GetStartMinute(dow), durationMinutes = dur, totalLogged = logged, totalSize = projected.TotalSize });
                scheduledIds.Add(projected.Id);
            }

            var blockItemIds = new HashSet<Guid>(schedBlocks.SelectMany(sb => sb.Items.Select(i => i.Id)));
            foreach (var n in allNodes.Where(n => n.IsActionable && n.ScheduleTemplate != null
                && (n.Status == ActionItemStatus.Active || n.Status == ActionItemStatus.NotStarted)
                && !scheduledIds.Contains(n.Id) && !blockItemIds.Contains(n.Id)))
            {
                var tmpl = ParseTemplate(n.ScheduleTemplate);
                if (tmpl is null || !tmpl.Days.Contains(dow)) continue;
                var dur = tmpl.GetDurationMinutes(dow);
                var planned = n.UnitsPerHour.HasValue ? (dur / 60.0) * n.UnitsPerHour.Value : 0;
                var logged = logTotals.GetValueOrDefault(n.Id, 0);
                blocks.Add(new { nodeId = n.Id, title = n.Title, path = BuildPath(n, lk), unit = n.Unit, plannedUnits = Math.Round(planned, 1), startMinute = tmpl.GetStartMinute(dow), durationMinutes = dur, totalLogged = logged, totalSize = n.TotalSize });
            }
        }

        return J(new
        {
            date = pd.ToString("yyyy-MM-dd"),
            isRelaxDay,
            activeSprint = new { sprint.Id, sprint.Name, startDate = sprint.StartDate.ToString("yyyy-MM-dd"), endDate = sprint.EndDate.ToString("yyyy-MM-dd") },
            blocks
        });
    }

    // ===== Work Logs =====

    [McpServerTool(Name = "log_work"), Description("Log work completed on an action item. Requires a started sprint covering that date. Auto-completes the node if totalSize is reached.")]
    public async Task<string> LogWork(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Node UUID (must be an action item)")] Guid node_id,
        [Description("Date (YYYY-MM-DD)")] string date,
        [Description("Amount of work units to log")] double amount,
        [Description("Optional note")] string? note = null)
    {
        if (!DateOnly.TryParse(date, out var pd)) return J(new { error = "Invalid date." });
        var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == node_id && n.RoadmapId == roadmap_id);
        if (node is null) return J(new { error = "Node not found" });
        if (!node.IsActionable) return J(new { error = "Node is not an action item." });

        var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.RoadmapId == roadmap_id && s.StartDate <= pd && s.EndDate >= pd && s.IsStarted);
        if (sprint is null) return J(new { error = "No started sprint covers this date. Start a sprint first." });

        var ex = await db.WorkLogs.FirstOrDefaultAsync(w => w.SprintId == sprint.Id && w.NodeId == node_id && w.Date == pd);
        if (ex != null) { ex.Amount += amount; ex.Note = note ?? ex.Note; }
        else db.WorkLogs.Add(new WorkLog { Id = Guid.NewGuid(), RoadmapId = roadmap_id, SprintId = sprint.Id, NodeId = node_id, Date = pd, Amount = amount, Note = note });
        await db.SaveChangesAsync();

        if (node.TotalSize.HasValue && node.Status != ActionItemStatus.Completed)
        {
            var totalLogged = await db.WorkLogs.Where(w => w.NodeId == node_id).SumAsync(w => w.Amount);
            if (totalLogged >= node.TotalSize.Value)
            {
                var old = node.Status;
                node.Status = ActionItemStatus.Completed;
                db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = roadmap_id, NodeId = node_id, OldStatus = old, NewStatus = ActionItemStatus.Completed, Trigger = "auto_completed_mcp" });
                await ActivateNextInQueue(node);
                await db.SaveChangesAsync();
                return J(new { success = true, autoCompleted = true });
            }
        }
        return J(new { success = true, autoCompleted = false });
    }

    [McpServerTool(Name = "get_work_logs"), Description("Get all work logs recorded for a specific date.")]
    public async Task<string> GetWorkLogs(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Date (YYYY-MM-DD)")] string date)
    {
        if (!DateOnly.TryParse(date, out var pd)) return J(new { error = "Invalid date." });
        var logs = await db.WorkLogs.AsNoTracking().Include(w => w.Node)
            .Where(w => w.RoadmapId == roadmap_id && w.Date == pd).OrderBy(w => w.CreatedAt).ToListAsync();
        return J(logs.Select(w => new WorkLogDto(w.Id, w.NodeId, w.Node.Title, w.Date.ToString("yyyy-MM-dd"), w.Amount, w.Node.Unit, w.Note)));
    }

    [McpServerTool(Name = "get_node_work_history"), Description("Get the complete work log history for a specific node across all sprints.")]
    public async Task<string> GetNodeWorkHistory(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Node UUID")] Guid node_id)
    {
        var node = await db.Nodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == node_id && n.RoadmapId == roadmap_id);
        if (node is null) return J(new { error = "Node not found" });
        var logs = await db.WorkLogs.AsNoTracking().Include(w => w.Sprint)
            .Where(w => w.NodeId == node_id && w.RoadmapId == roadmap_id)
            .OrderByDescending(w => w.Date).ToListAsync();
        return J(new WorkLogHistoryDto(node.Id, node.Title, node.Unit,
            logs.Select(w => new WorkLogHistoryEntryDto(w.Id, w.Date.ToString("yyyy-MM-dd"), w.Amount, w.Note, w.Sprint.Name)).ToList()));
    }

    // ===== Tasks =====

    [McpServerTool(Name = "list_tasks"), Description("List all single tasks, ordered by completion status, then priority, then creation date.")]
    public async Task<string> ListTasks([Description("Roadmap UUID")] Guid roadmap_id)
    {
        var all = await db.SingleTasks.AsNoTracking().Where(t => t.RoadmapId == roadmap_id)
            .OrderBy(t => t.IsCompleted).ThenBy(t => t.Priority).ThenBy(t => t.CreatedAt).ToListAsync();
        return J(all.Select(ToTaskDto));
    }

    [McpServerTool(Name = "create_task"), Description("Create a single (one-off) task. Earns floor(estimatedHours * 2) points when completed within a sprint.")]
    public async Task<string> CreateTask(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Task title")] string title,
        [Description("Priority: High, Medium, or Low")] string priority,
        [Description("Estimated hours to complete")] double estimated_hours,
        [Description("Date to start showing this task (YYYY-MM-DD)")] string start_date,
        [Description("Optional due date (YYYY-MM-DD)")] string? due_date = null)
    {
        if (!DateOnly.TryParse(start_date, out var sd)) return J(new { error = "Invalid start_date." });
        DateOnly? dd = null;
        if (due_date != null && DateOnly.TryParse(due_date, out var parsed)) dd = parsed;
        if (!Enum.TryParse<TaskPriority>(priority, true, out var pri)) pri = TaskPriority.Medium;
        var t = new SingleTask { Id = Guid.NewGuid(), RoadmapId = roadmap_id, Title = title.Trim(), Priority = pri, EstimatedHours = estimated_hours, StartDate = sd, DueDate = dd };
        db.SingleTasks.Add(t);
        await db.SaveChangesAsync();
        return J(ToTaskDto(t));
    }

    [McpServerTool(Name = "complete_task"), Description("Mark a task as completed on a specific date, crediting points to that date's sprint performance.")]
    public async Task<string> CompleteTask(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Task UUID")] Guid task_id,
        [Description("Completion date (YYYY-MM-DD)")] string date)
    {
        var t = await db.SingleTasks.FirstOrDefaultAsync(x => x.Id == task_id && x.RoadmapId == roadmap_id);
        if (t is null) return J(new { error = "Task not found" });
        if (!DateOnly.TryParse(date, out var cd)) return J(new { error = "Invalid date." });
        t.IsCompleted = true; t.CompletedDate = cd;
        await db.SaveChangesAsync();
        return J(new { success = true, points = Math.Floor(t.EstimatedHours * 2) });
    }

    [McpServerTool(Name = "delete_task"), Description("Delete a single task.")]
    public async Task<string> DeleteTask(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Task UUID")] Guid task_id)
    {
        var t = await db.SingleTasks.FirstOrDefaultAsync(x => x.Id == task_id && x.RoadmapId == roadmap_id);
        if (t is null) return J(new { error = "Task not found" });
        db.SingleTasks.Remove(t);
        await db.SaveChangesAsync();
        return J(new { success = true });
    }

    // ===== Habits =====

    [McpServerTool(Name = "list_habits"), Description("List all habits in the habit library for a roadmap.")]
    public async Task<string> ListHabits([Description("Roadmap UUID")] Guid roadmap_id)
    {
        var result = await db.Habits.AsNoTracking().Where(h => h.RoadmapId == roadmap_id)
            .OrderBy(h => h.CreatedAt).Select(h => new HabitDto(h.Id, h.Name, h.CreatedAt)).ToListAsync();
        return J(result);
    }

    [McpServerTool(Name = "create_habit"), Description("Create a new habit. Habits can be added to sprints for daily tracking (+2 pts checked, -2 pts missed).")]
    public async Task<string> CreateHabit(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Habit name")] string name)
    {
        var h = new Habit { Id = Guid.NewGuid(), RoadmapId = roadmap_id, Name = name.Trim() };
        db.Habits.Add(h);
        await db.SaveChangesAsync();
        return J(new HabitDto(h.Id, h.Name, h.CreatedAt));
    }

    // ===== Custom Logs =====

    [McpServerTool(Name = "create_custom_log"), Description("Log a custom activity with a manual point value (e.g. 'Morning run: 2 pts'). Points count toward sprint performance on that date.")]
    public async Task<string> CreateCustomLog(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Activity title")] string title,
        [Description("Points to award (can be negative)")] double points,
        [Description("Date (YYYY-MM-DD)")] string date,
        [Description("Optional note")] string? note = null)
    {
        if (!DateOnly.TryParse(date, out var pd)) return J(new { error = "Invalid date." });
        var c = new CustomLog { Id = Guid.NewGuid(), RoadmapId = roadmap_id, Title = title.Trim(), Points = points, Date = pd, Note = note };
        db.CustomLogs.Add(c);
        await db.SaveChangesAsync();
        return J(new CustomLogDto(c.Id, c.Title, c.Points, c.Date.ToString("yyyy-MM-dd"), c.Note));
    }

    // ===== Daily Notes =====

    [McpServerTool(Name = "add_note"), Description("Add a daily note to a book ('red' or 'green'). Date defaults to today (Asia/Yerevan). If an entry already exists for that book+date it appends the content as a new line; otherwise it creates a new entry with the next day_number. Returns { day_number, action }.")]
    public async Task<string> AddNote(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("Note content")] string content,
        [Description("Date (YYYY-MM-DD), defaults to today in Asia/Yerevan")] string? date = null)
    {
        if (!TryNormalizeBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        DateOnly entryDate;
        if (date is null) entryDate = YerevanToday();
        else if (!DateOnly.TryParse(date, out entryDate)) return J(new { error = "Invalid date. Use YYYY-MM-DD." });

        // Retry to absorb the rare race where a concurrent add wins the unique
        // (book, entry_date) / (book, day_number) constraint between our read and write.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var existing = await db.Notes.FirstOrDefaultAsync(n => n.Book == bk && n.EntryDate == entryDate);
            if (existing != null)
            {
                existing.Content = existing.Content.Length == 0 ? content : existing.Content + "\n" + content;
                existing.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return J(new { day_number = existing.DayNumber, action = "appended" });
            }

            var maxDay = await db.Notes.Where(n => n.Book == bk).MaxAsync(n => (int?)n.DayNumber) ?? 0;
            var note = new Note { Id = Guid.NewGuid(), Book = bk, DayNumber = maxDay + 1, EntryDate = entryDate, Content = content };
            db.Notes.Add(note);
            try
            {
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return J(new { day_number = note.DayNumber, action = "created" });
            }
            catch (DbUpdateException)
            {
                // A concurrent insert took this (book, entry_date) or day_number; undo and retry.
                await tx.RollbackAsync();
                db.Entry(note).State = EntityState.Detached;
            }
        }
        return J(new { error = "Could not add note due to concurrent updates. Please retry." });
    }

    [McpServerTool(Name = "get_note"), Description("Get the note entry for a specific day_number within a book.")]
    public async Task<string> GetNote(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The day_number of the entry")] int number)
    {
        if (!TryNormalizeBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        var note = await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Book == bk && n.DayNumber == number);
        if (note is null) return J(new { error = "Note not found" });
        return J(ToNoteDto(note));
    }

    [McpServerTool(Name = "get_recent_notes"), Description("Get the last n note entries for a book, ordered by day_number descending (most recent day first).")]
    public async Task<string> GetRecentNotes(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("Number of entries to return")] int n)
    {
        if (!TryNormalizeBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        if (n < 1) n = 1;
        var notes = await db.Notes.AsNoTracking().Where(x => x.Book == bk)
            .OrderByDescending(x => x.DayNumber).Take(n).ToListAsync();
        return J(notes.Select(ToNoteDto));
    }

    [McpServerTool(Name = "get_notes_by_numbers"), Description("Get all note entries in a book whose day_number falls within an inclusive range [from_number, to_number], ordered by day_number ascending. Gaps in the sequence are simply skipped.")]
    public async Task<string> GetNotesByNumbers(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("First day_number in the range (inclusive)")] int from_number,
        [Description("Last day_number in the range (inclusive)")] int to_number)
    {
        if (!TryNormalizeBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        var lo = Math.Min(from_number, to_number);
        var hi = Math.Max(from_number, to_number);
        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.Book == bk && n.DayNumber >= lo && n.DayNumber <= hi)
            .OrderBy(n => n.DayNumber).ToListAsync();
        return J(notes.Select(ToNoteDto));
    }

    [McpServerTool(Name = "get_notes_by_dates"), Description("Get all note entries in a book whose entry_date falls within an inclusive date range [from_date, to_date] (YYYY-MM-DD), ordered by entry_date ascending.")]
    public async Task<string> GetNotesByDates(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("Start date (YYYY-MM-DD), inclusive")] string from_date,
        [Description("End date (YYYY-MM-DD), inclusive")] string to_date)
    {
        if (!TryNormalizeBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        if (!DateOnly.TryParse(from_date, out var fd)) return J(new { error = "Invalid from_date. Use YYYY-MM-DD." });
        if (!DateOnly.TryParse(to_date, out var td)) return J(new { error = "Invalid to_date. Use YYYY-MM-DD." });
        if (td < fd) (fd, td) = (td, fd);
        var notes = await db.Notes.AsNoTracking()
            .Where(n => n.Book == bk && n.EntryDate >= fd && n.EntryDate <= td)
            .OrderBy(n => n.EntryDate).ToListAsync();
        return J(notes.Select(ToNoteDto));
    }

    [McpServerTool(Name = "update_note"), Description("Replace the full content of a day's note entry within a book. Overwrites (does not append). Returns the updated entry.")]
    public async Task<string> UpdateNote(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The day_number of the entry to overwrite")] int number,
        [Description("The new full content for that day")] string content)
    {
        if (!TryNormalizeBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Book == bk && n.DayNumber == number);
        if (note is null) return J(new { error = "Note not found" });
        note.Content = content;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(ToNoteDto(note));
    }

    [McpServerTool(Name = "delete_note"), Description("Delete a day's note entry from a book by its day_number. Leaves a gap in the day_number sequence (following days are not renumbered). Returns { deleted, day_number }.")]
    public async Task<string> DeleteNote(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The day_number of the entry to delete")] int number)
    {
        if (!TryNormalizeBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Book == bk && n.DayNumber == number);
        if (note is null) return J(new { error = "Note not found" });
        db.Notes.Remove(note);
        await db.SaveChangesAsync();
        return J(new { deleted = true, day_number = number });
    }

    [McpServerTool(Name = "delete_note_lines"), Description("Delete specific bullet lines from a day's note entry within a book, addressed by their 1-based line numbers (as returned in the entry's content). Remaining lines keep their order and are renumbered. Returns the updated entry and the lines that were removed.")]
    public async Task<string> DeleteNoteLines(
        [Description("Book: 'red' or 'green'")] string book,
        [Description("The day_number of the entry")] int number,
        [Description("1-based line numbers of the bullets to delete")] int[] line_numbers)
    {
        if (!TryNormalizeBook(book, out var bk)) return J(new { error = "Invalid book. Use 'red' or 'green'." });
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Book == bk && n.DayNumber == number);
        if (note is null) return J(new { error = "Note not found" });

        var lines = note.Content.Split('\n');
        var toRemove = new HashSet<int>(line_numbers.Select(i => i - 1));
        var invalid = line_numbers.Where(i => i < 1 || i > lines.Length).ToList();
        if (invalid.Count > 0)
            return J(new { error = $"Line number(s) out of range for this entry (has {lines.Length} line(s)): {string.Join(", ", invalid)}." });

        var removed = lines.Where((_, idx) => toRemove.Contains(idx)).ToList();
        var kept = lines.Where((_, idx) => !toRemove.Contains(idx)).ToList();

        note.Content = string.Join("\n", kept);
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return J(new { note = ToNoteDto(note), removed, removedCount = removed.Count, remainingLines = kept.Count });
    }

    // ===== Job scouting =====
    // Ingestion path for the Finder pipeline: it scouts + filters, an LLM scores,
    // and the result lands here as one run per day, surfaced by the Jobs tab.

    [McpServerTool(Name = "import_job_run"), Description(
        "Import a day's job-scouting results from the Finder pipeline. Creates one run for the given date " +
        "and attaches the postings. Re-importing the same date REPLACES that day's postings (idempotent — " +
        "safe to re-run the scout after re-scoring). Postings should be passed in the order you want them " +
        "shown; the Jobs tab pages through them in that order, so put the best candidates first. " +
        "Returns { run_id, run_date, imported, action }.")]
    public async Task<string> ImportJobRun(
        [Description("Postings to import, best-first. Each: title, company, url, source, bucket ('armenia-compatible' or 'eu-allowed'), and optionally location, posted_at (YYYY-MM-DD), description, seniority_class, ai_keyword_hits, geo_hints, queries, score (0-100), reasoning, cv_pdf_base64 (a tailored ATS-ready PDF résumé, base64-encoded), cv_changes (a short list of what that CV changed vs the master CV).")]
        JobPostingInput[] postings,
        [Description("The queries the scout ran, e.g. ['senior backend engineer','staff backend engineer']")]
        string[] queries,
        [Description("Total postings ingested across all sources before filtering")] int raw_count,
        [Description("Run date (YYYY-MM-DD), defaults to today in Asia/Yerevan")] string? run_date = null,
        [Description("Max posting age in days the run filtered on. Defaults to 14.")] int max_age_days = 14)
    {
        DateOnly date;
        if (run_date is null) date = YerevanToday();
        else if (!DateOnly.TryParse(run_date, out date)) return J(new { error = "Invalid run_date. Use YYYY-MM-DD." });

        if (postings.Length == 0) return J(new { error = "No postings supplied." });

        foreach (var p in postings)
        {
            if (string.IsNullOrWhiteSpace(p.title) || string.IsNullOrWhiteSpace(p.company) || string.IsNullOrWhiteSpace(p.url))
                return J(new { error = "Every posting needs a title, company, and url." });
            if (!ValidBuckets.Contains(p.bucket ?? ""))
                return J(new { error = $"Invalid bucket '{p.bucket}' for '{p.title}'. Use 'armenia-compatible' or 'eu-allowed'." });
        }

        var existing = await db.JobRuns.Include(r => r.Postings).FirstOrDefaultAsync(r => r.RunDate == date);
        var action = existing is null ? "created" : "replaced";

        JobRun run;
        if (existing is not null)
        {
            db.JobPostings.RemoveRange(existing.Postings); // cascade would too, but be explicit
            existing.Queries = [.. queries];
            existing.MaxAgeDays = max_age_days;
            existing.RawCount = raw_count;
            run = existing;
        }
        else
        {
            run = new JobRun
            {
                Id = Guid.NewGuid(), RunDate = date, Queries = [.. queries],
                MaxAgeDays = max_age_days, RawCount = raw_count,
            };
            db.JobRuns.Add(run);
        }

        var order = 0;
        foreach (var p in postings)
        {
            DateOnly? posted = DateOnly.TryParse(p.posted_at, out var pd) ? pd : null;
            db.JobPostings.Add(new JobPosting
            {
                Id = Guid.NewGuid(),
                JobRunId = run.Id,
                Title = Trunc(p.title!, 512),
                Company = Trunc(p.company!, 256),
                Url = Trunc(p.url!, 1024),
                Source = Trunc(p.source ?? "unknown", 64),
                Location = p.location is null ? null : Trunc(p.location, 512),
                PostedAt = posted,
                Description = p.description ?? "",
                Bucket = p.bucket!,
                SeniorityClass = p.seniority_class is null ? null : Trunc(p.seniority_class, 64),
                AiKeywordHits = p.ai_keyword_hits ?? 0,
                GeoHints = [.. p.geo_hints ?? []],
                Queries = [.. p.queries ?? []],
                Score = p.score,
                Reasoning = p.reasoning,
                TailoredCvPdf = DecodeCvPdf(p.cv_pdf_base64),
                CvChangeList = string.IsNullOrWhiteSpace(p.cv_changes) ? null : p.cv_changes,
                SortOrder = order++,
            });
        }

        await db.SaveChangesAsync();
        return J(new { run_id = run.Id, run_date = date.ToString("yyyy-MM-dd"), imported = postings.Length, action });
    }

    [McpServerTool(Name = "list_job_runs"), Description("List job-scouting runs, most recent day first, with posting counts. Use this to see which days have results.")]
    public async Task<string> ListJobRuns([Description("How many runs to return. Defaults to 30.")] int n = 30)
    {
        if (n < 1) n = 1;
        var runs = await db.JobRuns.AsNoTracking()
            .OrderByDescending(r => r.RunDate).Take(n)
            .Select(r => new JobRunSummaryDto(r.Id, r.RunDate.ToString("yyyy-MM-dd"), r.Queries,
                r.MaxAgeDays, r.RawCount, r.Postings.Count, r.CreatedAt))
            .ToListAsync();
        return J(runs);
    }

    [McpServerTool(Name = "get_job_run"), Description("Get one day's job postings in full, including descriptions and scores. Omit the date to get the most recent run.")]
    public async Task<string> GetJobRun([Description("Run date (YYYY-MM-DD). Omit for the most recent run.")] string? date = null)
    {
        JobRun? run;
        if (date is null)
        {
            run = await db.JobRuns.AsNoTracking().Include(r => r.Postings)
                .OrderByDescending(r => r.RunDate).FirstOrDefaultAsync();
        }
        else
        {
            if (!DateOnly.TryParse(date, out var d)) return J(new { error = "Invalid date. Use YYYY-MM-DD." });
            run = await db.JobRuns.AsNoTracking().Include(r => r.Postings)
                .FirstOrDefaultAsync(r => r.RunDate == d);
        }
        if (run is null) return J(new { error = "No job run found" });

        return J(new JobRunDto(run.Id, run.RunDate.ToString("yyyy-MM-dd"), run.Queries, run.MaxAgeDays,
            run.RawCount, run.CreatedAt,
            run.Postings.OrderBy(p => p.SortOrder).Select(p => new JobPostingDto(
                p.Id, p.Title, p.Company, p.Url, p.Source, p.Location,
                p.PostedAt?.ToString("yyyy-MM-dd"), p.Description, p.Bucket,
                p.SeniorityClass, p.AiKeywordHits, p.GeoHints, p.Queries,
                p.Score, p.Reasoning, p.SortOrder,
                p.TailoredCvPdf != null && p.TailoredCvPdf.Length > 0, p.CvChangeList)).ToList()));
    }

    private static readonly HashSet<string> ValidBuckets = ["armenia-compatible", "eu-allowed"];

    // The DB caps these columns; a scraped title or location can exceed them (HN
    // comments in particular are unbounded). Truncate rather than fail the import.
    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max];

    // A tailored CV arrives as base64 (MCP args are JSON). Decode leniently: bad or
    // empty input just means "no CV", never a failed import.
    private static byte[]? DecodeCvPdf(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        try
        {
            var bytes = Convert.FromBase64String(base64.Trim());
            return bytes.Length == 0 ? null : bytes;
        }
        catch (FormatException) { return null; }
    }

    private static bool TryNormalizeBook(string? book, out string normalized)
    {
        normalized = book?.Trim().ToLowerInvariant() ?? "";
        return normalized is "red" or "green";
    }

    // Armenia (Asia/Yerevan) is UTC+4 year-round (no DST since 2012). Resolve via the
    // tz database when available, but fall back to a fixed offset since the alpine
    // runtime image ships without tzdata and InvariantGlobalization is enabled.
    private static readonly TimeZoneInfo YerevanTz = ResolveYerevanTz();
    private static TimeZoneInfo ResolveYerevanTz()
    {
        foreach (var id in new[] { "Asia/Yerevan", "Caucasus Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.CreateCustomTimeZone("Yerevan+4", TimeSpan.FromHours(4), "Yerevan", "Yerevan");
    }
    private static DateOnly YerevanToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, YerevanTz));

    private static NoteDto ToNoteDto(Note n) => new(n.Book, n.DayNumber, n.EntryDate.ToString("yyyy-MM-dd"), n.Content, n.CreatedAt, n.UpdatedAt);

    // ===== Private helpers (ported from RoadmapEndpoints) =====

    private static string BuildPath(RoadmapNode n, Dictionary<Guid, RoadmapNode> lk)
    {
        var p = new List<string>(); var c = n;
        while (c != null) { p.Add(c.Title); c = c.ParentId.HasValue && lk.TryGetValue(c.ParentId.Value, out var pr) ? pr : null; }
        p.Reverse(); return string.Join(" / ", p);
    }

    private static List<NodeDto> BuildTree(List<RoadmapNode> all, List<NodeCategoryLink> links, Dictionary<Guid, RoadmapNode> lk, Guid? pid) =>
        all.Where(n => n.ParentId == pid).OrderBy(n => n.SortOrder).Select(n =>
        {
            var cl = links.Where(l => l.NodeId == n.Id).Select(l => new CategoryLinkDto(l.Id, l.CategoryId, lk.TryGetValue(l.CategoryId, out var c) ? c.Title : "?")).ToList();
            return new NodeDto(n.Id, n.ParentId, n.Title, n.IsActionable, n.Status.ToString(), n.Unit, n.TotalSize, n.UnitsPerHour, n.PointsPerUnit, n.ScheduleTemplate, n.SortOrder, n.ScheduleBlockId, n.BlockSortOrder, cl, BuildTree(all, links, lk, n.Id), n.IsChecklist);
        }).ToList();

    private static SingleTaskDto ToTaskDto(SingleTask t) => new(t.Id, t.Title, t.Priority.ToString(),
        t.EstimatedHours, t.Weekdays, t.StartDate.ToString("yyyy-MM-dd"),
        t.DueDate?.ToString("yyyy-MM-dd"), t.DelayedUntil?.ToString("yyyy-MM-dd"),
        t.IsCompleted, t.CompletedDate?.ToString("yyyy-MM-dd"), Math.Floor(t.EstimatedHours * 2));

    private async Task ActivateNextInQueue(RoadmapNode completedNode)
    {
        if (completedNode.ScheduleBlockId.HasValue)
        {
            var siblings = await db.Nodes.Where(n => n.ScheduleBlockId == completedNode.ScheduleBlockId && n.IsActionable && n.Id != completedNode.Id
                && n.Status != ActionItemStatus.Completed && n.Status != ActionItemStatus.Stopped).OrderBy(n => n.BlockSortOrder).ToListAsync();
            var next = siblings.FirstOrDefault(n => n.BlockSortOrder > completedNode.BlockSortOrder) ?? siblings.FirstOrDefault();
            if (next != null && next.Status == ActionItemStatus.NotStarted)
            {
                var old = next.Status; next.Status = ActionItemStatus.Active;
                db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = next.RoadmapId, NodeId = next.Id, OldStatus = old, NewStatus = ActionItemStatus.Active, Trigger = "auto_queue_mcp" });
            }
            return;
        }
        if (completedNode.ParentId is null) return;
        var sibs = await db.Nodes.Where(n => n.ParentId == completedNode.ParentId && n.IsActionable && n.Id != completedNode.Id
            && n.Status != ActionItemStatus.Completed && n.Status != ActionItemStatus.Stopped).OrderBy(n => n.SortOrder).ToListAsync();
        var nextSib = sibs.FirstOrDefault(n => n.SortOrder > completedNode.SortOrder) ?? sibs.FirstOrDefault();
        if (nextSib != null && nextSib.Status == ActionItemStatus.NotStarted)
        {
            var old = nextSib.Status; nextSib.Status = ActionItemStatus.Active;
            db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = nextSib.RoadmapId, NodeId = nextSib.Id, OldStatus = old, NewStatus = ActionItemStatus.Active, Trigger = "auto_queue_mcp" });
        }
    }

    private record DayOverride(int StartMinute, int DurationMinutes);
    private record TemplateData(List<int> Days, int StartMinute, int DurationMinutes, Dictionary<int, DayOverride>? PerDay)
    {
        public int GetStartMinute(int dow) => PerDay != null && PerDay.TryGetValue(dow, out var o) ? o.StartMinute : StartMinute;
        public int GetDurationMinutes(int dow) => PerDay != null && PerDay.TryGetValue(dow, out var o) ? o.DurationMinutes : DurationMinutes;
    }

    private static TemplateData? ParseTemplate(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json); var r = doc.RootElement;
            var days = r.GetProperty("days").EnumerateArray().Select(e => e.GetInt32()).ToList();
            var startMinute = r.GetProperty("startMinute").GetInt32();
            var durationMinutes = r.GetProperty("durationMinutes").GetInt32();
            Dictionary<int, DayOverride>? perDay = null;
            if (r.TryGetProperty("perDay", out var pd) && pd.ValueKind == JsonValueKind.Object)
            {
                perDay = [];
                foreach (var prop in pd.EnumerateObject())
                    if (int.TryParse(prop.Name, out var dayNum))
                    {
                        var sm = prop.Value.TryGetProperty("startMinute", out var smv) ? smv.GetInt32() : startMinute;
                        var dm = prop.Value.TryGetProperty("durationMinutes", out var dmv) ? dmv.GetInt32() : durationMinutes;
                        perDay[dayNum] = new DayOverride(sm, dm);
                    }
            }
            return new TemplateData(days, startMinute, durationMinutes, perDay);
        }
        catch { return null; }
    }

    private record ComputedPlanEntry(Guid NodeId, DateOnly Date, int StartMinute, int DurationMinutes, double PlannedUnits);

    private static List<ComputedPlanEntry> ComputeSprintPlan(
        List<RoadmapNode> allNodes, List<ScheduleBlock> blocks, List<DateOnly> dates,
        Dictionary<Guid, double> allTimeLogged, HashSet<string>? relaxDays = null)
    {
        var entries = new List<ComputedPlanEntry>();
        var blockItemIds = new HashSet<Guid>();
        var relaxSet = relaxDays ?? [];

        foreach (var block in blocks)
        {
            var blockTmpl = ParseTemplate(block.ScheduleTemplate); if (blockTmpl is null) continue;
            var queue = block.Items
                .Where(n => n.IsActionable && (n.Status == ActionItemStatus.Active || n.Status == ActionItemStatus.NotStarted))
                .OrderBy(n => n.BlockSortOrder).ToList();
            if (queue.Count == 0) continue;
            foreach (var item in queue) blockItemIds.Add(item.Id);
            int qi = 0;
            double remainingForCurrent = GetRemaining(queue[qi], allTimeLogged);
            foreach (var date in dates)
            {
                var ddow = (int)date.DayOfWeek;
                if (!blockTmpl.Days.Contains(ddow)) continue;
                if (relaxSet.Contains(date.ToString("yyyy-MM-dd"))) continue;
                if (qi >= queue.Count) break;
                var item = queue[qi];
                var dur = blockTmpl.GetDurationMinutes(ddow);
                var rawPlanned = item.UnitsPerHour.HasValue ? (dur / 60.0) * item.UnitsPerHour.Value : 0;
                var actualPlanned = Math.Min(rawPlanned, remainingForCurrent);
                if (actualPlanned > 0)
                    entries.Add(new ComputedPlanEntry(item.Id, date, blockTmpl.GetStartMinute(ddow), dur, Math.Round(actualPlanned, 2)));
                remainingForCurrent -= actualPlanned;
                if (remainingForCurrent <= 0.01 && qi + 1 < queue.Count)
                {
                    qi++;
                    remainingForCurrent = GetRemaining(queue[qi], allTimeLogged);
                }
            }
        }

        foreach (var n in allNodes.Where(n => n.IsActionable && n.ScheduleTemplate != null
            && !blockItemIds.Contains(n.Id)
            && (n.Status == ActionItemStatus.Active || n.Status == ActionItemStatus.NotStarted)))
        {
            var tmpl = ParseTemplate(n.ScheduleTemplate); if (tmpl is null) continue;
            var remaining = GetRemaining(n, allTimeLogged);
            foreach (var date in dates)
            {
                if (remaining <= 0.01) break;
                var ddow = (int)date.DayOfWeek;
                if (!tmpl.Days.Contains(ddow)) continue;
                if (relaxSet.Contains(date.ToString("yyyy-MM-dd"))) continue;
                var dur = tmpl.GetDurationMinutes(ddow);
                var rawPlanned = n.UnitsPerHour.HasValue ? (dur / 60.0) * n.UnitsPerHour.Value : 0;
                var actualPlanned = Math.Min(rawPlanned, remaining);
                entries.Add(new ComputedPlanEntry(n.Id, date, tmpl.GetStartMinute(ddow), dur, Math.Round(actualPlanned, 2)));
                remaining -= actualPlanned;
            }
        }
        return entries;
    }

    private static double GetRemaining(RoadmapNode item, Dictionary<Guid, double> allTimeLogged)
    {
        if (!item.TotalSize.HasValue) return double.MaxValue;
        return Math.Max(0, item.TotalSize.Value - allTimeLogged.GetValueOrDefault(item.Id, 0));
    }

    private static RoadmapNode? ProjectBlockQueueToDate(List<RoadmapNode> queue, TemplateData blockTmpl,
        DateOnly targetDate, DateOnly baseDate, Dictionary<Guid, HashSet<DateOnly>> workLogDates, DateOnly today)
    {
        if (queue.Count == 0) return null;
        var itemSessions = new List<(RoadmapNode Node, int SessionsNeeded)>();
        foreach (var item in queue)
        {
            if (!item.TotalSize.HasValue || !item.UnitsPerHour.HasValue || item.UnitsPerHour.Value == 0)
            { itemSessions.Add((item, int.MaxValue)); continue; }
            var dur = blockTmpl.DurationMinutes;
            var unitsPerSession = (dur / 60.0) * item.UnitsPerHour.Value;
            var sessionsNeeded = unitsPerSession > 0 ? (int)Math.Ceiling(item.TotalSize.Value / unitsPerSession) : 0;
            if (sessionsNeeded <= 0) continue;
            itemSessions.Add((item, sessionsNeeded));
        }
        if (itemSessions.Count == 0) return null;
        int queueIdx = 0; int sessionsConsumed = 0;
        var maxDate = baseDate.AddDays(365);
        for (var d = baseDate; d <= maxDate && d <= targetDate.AddDays(1); d = d.AddDays(1))
        {
            if (!blockTmpl.Days.Contains((int)d.DayOfWeek)) continue;
            if (queueIdx >= itemSessions.Count) return null;
            if (d == targetDate) return itemSessions[queueIdx].Node;
            var currentNodeId = itemSessions[queueIdx].Node.Id;
            bool workedThisDay = d >= today || (workLogDates.TryGetValue(currentNodeId, out var dates) && dates.Contains(d));
            if (workedThisDay)
            {
                sessionsConsumed++;
                if (sessionsConsumed >= itemSessions[queueIdx].SessionsNeeded)
                { queueIdx++; sessionsConsumed = 0; }
            }
        }
        return queueIdx < itemSessions.Count ? itemSessions[queueIdx].Node : null;
    }
}

/// <summary>
/// One posting as supplied to import_job_run. Property names are snake_case so the
/// generated JSON-schema arg names match the rest of the MCP surface. Everything
/// except title/company/url/bucket is optional — the Finder pipeline fills the
/// signals in, and score/reasoning are added by whatever LLM did the scoring.
/// </summary>
public sealed record JobPostingInput(
    string? title,
    string? company,
    string? url,
    string? bucket,
    string? source = null,
    string? location = null,
    string? posted_at = null,
    string? description = null,
    string? seniority_class = null,
    int? ai_keyword_hits = null,
    string[]? geo_hints = null,
    string[]? queries = null,
    double? score = null,
    string? reasoning = null,
    // Optional tailored CV for this posting: a base64-encoded ATS-ready PDF and a
    // short human-readable list of what it changed vs. the master CV.
    string? cv_pdf_base64 = null,
    string? cv_changes = null);
