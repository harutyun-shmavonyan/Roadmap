using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Endpoints;
using Roadmap.Api.Entities;

namespace Roadmap.Api;

/// <summary>
/// Membership and order of schedule blocks — which items a block holds and the order a queue
/// block works through them — shared by the REST endpoints and the MCP tools so the two cannot
/// drift on what an assignment does (clear the item's own schedule, activate it, re-plan).
///
/// Every write re-plans the running sprints. A queue block's membership and order are followed
/// by the commitment as well as the day view (see <c>ReseatQueues</c>); a pool's membership is
/// followed by the day view only.
/// </summary>
public static class ScheduleBlockLogic
{
    public enum Outcome { Ok, NotFound, Invalid }

    public readonly record struct Result(Outcome Outcome, string? Error = null)
    {
        public static Result Ok => new(Outcome.Ok);
        public static Result NotFound(string what) => new(Outcome.NotFound, what);
        public static Result Invalid(string why) => new(Outcome.Invalid, why);
    }

    /// <summary>
    /// Put an action item into a block at a 0-based position in its order (null or past the end =
    /// last). Moves it out of any other block; if it is already in this block it just moves to the
    /// position. The block now owns its schedule, so the item's own schedule is cleared, and a
    /// not-started item becomes active. An item new to a pool joins it in play.
    /// </summary>
    public static async Task<Result> AssignAsync(RoadmapDbContext db, Guid roadmapId, Guid blockId, Guid nodeId, int? position)
    {
        if (!await db.ScheduleBlocks.AnyAsync(b => b.Id == blockId && b.RoadmapId == roadmapId)) return Result.NotFound("block not found");
        var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId);
        if (node is null) return Result.NotFound("item not found");
        if (!node.IsActionable) return Result.Invalid("only action items can go in a block, not categories");

        var others = await db.Nodes.Where(n => n.ScheduleBlockId == blockId && n.Id != nodeId)
            .OrderBy(n => n.BlockSortOrder).ThenBy(n => n.SortOrder).ToListAsync();
        var at = Math.Clamp(position ?? others.Count, 0, others.Count);
        others.Insert(at, node);
        for (var i = 0; i < others.Count; i++) others[i].BlockSortOrder = i;

        if (node.ScheduleBlockId != blockId) node.IsActiveInBlock = true;
        node.ScheduleBlockId = blockId;
        node.ScheduleTemplate = null;
        if (node.Status == ActionItemStatus.NotStarted)
        {
            db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = roadmapId, NodeId = node.Id,
                OldStatus = node.Status, NewStatus = ActionItemStatus.Active, Trigger = "block_assign" });
            node.Status = ActionItemStatus.Active;
        }
        await db.SaveChangesAsync();
        await RoadmapEndpoints.ReplanStartedSprintsAsync(db, roadmapId);
        return Result.Ok;
    }

    /// <summary>
    /// Take an item out of its block. With <paramref name="blockId"/> it must be in that block.
    /// The item is left unscheduled: it keeps its status but has no sessions until it is given a
    /// block or a schedule of its own.
    /// </summary>
    public static async Task<Result> RemoveAsync(RoadmapDbContext db, Guid roadmapId, Guid nodeId, Guid? blockId = null)
    {
        var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId);
        if (node is null || node.ScheduleBlockId is null || (blockId is Guid b && node.ScheduleBlockId != b))
            return Result.NotFound("item is not in that block");
        node.ScheduleBlockId = null;
        node.BlockSortOrder = 0;
        await db.SaveChangesAsync();
        await RoadmapEndpoints.ReplanStartedSprintsAsync(db, roadmapId);
        return Result.Ok;
    }

    /// <summary>
    /// Set a block's order: <paramref name="nodeIds"/> first, in that order, then any member not
    /// listed, in the order it already had. Every id must be a member, at most once.
    /// </summary>
    public static async Task<Result> ReorderAsync(RoadmapDbContext db, Guid roadmapId, Guid blockId, IReadOnlyList<Guid> nodeIds)
    {
        if (!await db.ScheduleBlocks.AnyAsync(b => b.Id == blockId && b.RoadmapId == roadmapId)) return Result.NotFound("block not found");
        var members = await db.Nodes.Where(n => n.ScheduleBlockId == blockId)
            .OrderBy(n => n.BlockSortOrder).ThenBy(n => n.SortOrder).ToListAsync();
        var byId = members.ToDictionary(n => n.Id);
        var foreign = nodeIds.Where(id => !byId.ContainsKey(id)).ToList();
        if (foreign.Count > 0) return Result.Invalid($"not in this block: {string.Join(", ", foreign)}");
        if (nodeIds.Distinct().Count() != nodeIds.Count) return Result.Invalid("an item is listed twice");

        var listed = nodeIds.ToHashSet();
        var order = nodeIds.Select(id => byId[id]).Concat(members.Where(n => !listed.Contains(n.Id))).ToList();
        for (var i = 0; i < order.Count; i++) order[i].BlockSortOrder = i;
        await db.SaveChangesAsync();
        await RoadmapEndpoints.ReplanStartedSprintsAsync(db, roadmapId);
        return Result.Ok;
    }

    /// <summary>
    /// Put pool items in or out of play. Only the listed items change; each must be a member of
    /// the block, and the block must be a pool (a queue ignores the flag, so setting it there
    /// would look like it did something and do nothing).
    /// </summary>
    public static async Task<Result> SetActiveAsync(RoadmapDbContext db, Guid roadmapId, Guid blockId, IReadOnlyList<Guid> nodeIds, bool active)
    {
        var block = await db.ScheduleBlocks.FirstOrDefaultAsync(b => b.Id == blockId && b.RoadmapId == roadmapId);
        if (block is null) return Result.NotFound("block not found");
        if (block.Mode != ScheduleBlockMode.Pool) return Result.Invalid("only pool blocks have items in and out of play; a queue block works through its items in order");
        var ids = nodeIds.ToHashSet();
        var items = await db.Nodes.Where(n => n.ScheduleBlockId == blockId && ids.Contains(n.Id)).ToListAsync();
        var foreign = ids.Except(items.Select(n => n.Id)).ToList();
        if (foreign.Count > 0) return Result.Invalid($"not in this block: {string.Join(", ", foreign)}");
        foreach (var n in items) n.IsActiveInBlock = active;
        await db.SaveChangesAsync();
        await RoadmapEndpoints.ReplanStartedSprintsAsync(db, roadmapId);
        return Result.Ok;
    }

    // ── describing ──

    static readonly string[] DayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    /// <summary>
    /// A block's schedule in words, e.g. "Mon, Wed 07:00–08:00; Sat 10:00–11:30" — the days that
    /// share a time are grouped. Null when the template is missing or unreadable.
    /// </summary>
    public static string? ScheduleText(string? template)
    {
        if (string.IsNullOrEmpty(template)) return null;
        try
        {
            using var doc = JsonDocument.Parse(template);
            var r = doc.RootElement;
            var start = r.GetProperty("startMinute").GetInt32();
            var dur = r.GetProperty("durationMinutes").GetInt32();
            var slots = r.GetProperty("days").EnumerateArray().Select(e => e.GetInt32()).Distinct()
                .OrderBy(d => (d + 6) % 7)   // Monday first
                .Select(d =>
                {
                    int s = start, m = dur;
                    if (r.TryGetProperty("perDay", out var pd) && pd.ValueKind == JsonValueKind.Object
                        && pd.TryGetProperty(d.ToString(), out var o))
                    {
                        if (o.TryGetProperty("startMinute", out var sv)) s = sv.GetInt32();
                        if (o.TryGetProperty("durationMinutes", out var dv)) m = dv.GetInt32();
                    }
                    return (Day: d, Time: $"{Clock(s)}–{Clock(s + m)}");
                }).ToList();
            if (slots.Count == 0) return null;
            return string.Join("; ", slots.GroupBy(x => x.Time)
                .Select(g => $"{string.Join(", ", g.Select(x => DayNames[x.Day % 7]))} {g.Key}"));
        }
        catch { return null; }
    }

    static string Clock(int minute) => $"{minute / 60 % 24:00}:{minute % 60:00}";

    /// <summary>
    /// Every block with its members in order, for an assistant to read before moving anything. In
    /// a queue, <c>isNext</c> marks the item the block is working on now: the first one still
    /// active or not started.
    /// </summary>
    public static async Task<List<object>> DescribeAsync(RoadmapDbContext db, Guid roadmapId, Guid? onlyBlockId = null)
    {
        var blocks = await db.ScheduleBlocks.AsNoTracking().Include(b => b.Items)
            .Where(b => b.RoadmapId == roadmapId && (onlyBlockId == null || b.Id == onlyBlockId))
            .OrderBy(b => b.SortOrder).ToListAsync();
        var ids = blocks.SelectMany(b => b.Items.Select(i => i.Id)).ToList();
        var logged = await db.WorkLogs.AsNoTracking().Where(w => ids.Contains(w.NodeId))
            .GroupBy(w => w.NodeId).Select(g => new { g.Key, Total = g.Sum(w => w.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);

        static bool Open(RoadmapNode n) => n.IsActionable
            && (n.Status == ActionItemStatus.Active || n.Status == ActionItemStatus.NotStarted);

        return blocks.Select(b =>
        {
            var items = b.Items.OrderBy(i => i.BlockSortOrder).ThenBy(i => i.SortOrder).ToList();
            var next = b.Mode == ScheduleBlockMode.Queue ? items.FirstOrDefault(Open)?.Id : null;
            return (object)new
            {
                blockId = b.Id,
                name = b.Name,
                mode = b.Mode.ToString(),
                schedule = ScheduleText(b.ScheduleTemplate),
                items = items.Select((n, i) => new
                {
                    position = i + 1,
                    nodeId = n.Id,
                    title = n.Title,
                    status = n.Status.ToString(),
                    unit = n.Unit,
                    totalSize = n.TotalSize,
                    totalLogged = Math.Round(logged.GetValueOrDefault(n.Id, 0), 2),
                    isNext = b.Mode == ScheduleBlockMode.Queue ? n.Id == next : (bool?)null,
                    inPlay = b.Mode == ScheduleBlockMode.Pool ? n.IsActiveInBlock : (bool?)null,
                }).ToList(),
            };
        }).ToList();
    }
}
