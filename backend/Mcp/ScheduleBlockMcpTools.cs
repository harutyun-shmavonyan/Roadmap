using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Roadmap.Api.Data;

namespace Roadmap.Api.Mcp;

/// <summary>
/// Schedule blocks — the recurring time slots of the day view — and which action items they
/// hold, in what order. The same writes the tab's block editor makes, through the same
/// <see cref="ScheduleBlockLogic"/>. Each tool answers with the block as it now stands, so the
/// result can be checked without a second read.
/// </summary>
[McpServerToolType]
public sealed class ScheduleBlockMcpTools(RoadmapDbContext db)
{
    static readonly JsonSerializerOptions Out = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    static string J(object? v) => JsonSerializer.Serialize(v, Out);

    const string Modes =
        "A Queue block works through its items in order: the first one still active or not started (isNext) gets every " +
        "session until it is done, then the next takes over. A Pool block's items share its sessions and the item is " +
        "picked when logging; only items in play (inPlay) count.";

    async Task<string> Answer(ScheduleBlockLogic.Result r, Guid roadmapId, Guid blockId, string status) =>
        r.Outcome == ScheduleBlockLogic.Outcome.Ok
            ? J(new { status, block = (await ScheduleBlockLogic.DescribeAsync(db, roadmapId, blockId)).FirstOrDefault() })
            : J(new { error = r.Error });

    [McpServerTool(Name = "list_schedule_blocks"), Description(
        "List the roadmap's schedule blocks — the recurring time slots of the daily schedule — with their days and times " +
        "and the action items each holds, in order (position 1 = first). " + Modes + " Use this to find block and item " +
        "ids before assign_to_schedule_block, reorder_schedule_block, remove_from_schedule_block or set_pool_items_in_play.")]
    public async Task<string> ListBlocks([Description("Roadmap UUID")] Guid roadmap_id) =>
        J(await ScheduleBlockLogic.DescribeAsync(db, roadmap_id));

    [McpServerTool(Name = "assign_to_schedule_block"), Description(
        "Put an action item into a schedule block, or move it within one. The item takes the block's schedule (its own " +
        "schedule is cleared), leaves any other block it was in, and becomes Active if it was NotStarted. position is " +
        "1-based in the block's order (1 = first, i.e. worked on next in a queue); omit it to add at the end. Called on an " +
        "item already in the block, it just moves it to that position. Re-plans the running sprint.")]
    public async Task<string> Assign(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Schedule block UUID (see list_schedule_blocks)")] Guid block_id,
        [Description("Action item UUID")] Guid node_id,
        [Description("1-based position in the block's order; default last")] int? position = null)
    {
        if (position is < 1) return J(new { error = "position is 1-based: 1 is first" });
        var r = await ScheduleBlockLogic.AssignAsync(db, roadmap_id, block_id, node_id, position - 1);
        return await Answer(r, roadmap_id, block_id, "assigned");
    }

    [McpServerTool(Name = "reorder_schedule_block"), Description(
        "Set the order of a schedule block's items. Give the item ids in the order wanted; any member left out keeps its " +
        "relative order after the ones listed, so naming just the first few is enough to bring them to the front. In a " +
        "queue block the order decides which item gets the sessions next. Re-plans the running sprint.")]
    public async Task<string> Reorder(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Schedule block UUID")] Guid block_id,
        [Description("Item UUIDs in the wanted order; each must be in this block")] Guid[] node_ids)
    {
        if (node_ids.Length == 0) return J(new { error = "give at least one item id" });
        var r = await ScheduleBlockLogic.ReorderAsync(db, roadmap_id, block_id, node_ids);
        return await Answer(r, roadmap_id, block_id, "reordered");
    }

    [McpServerTool(Name = "remove_from_schedule_block"), Description(
        "Take an action item out of its schedule block. It keeps its status but has no scheduled sessions until it is " +
        "assigned to a block again. Re-plans the running sprint.")]
    public async Task<string> Remove(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Action item UUID")] Guid node_id)
    {
        var blockId = await db.Nodes.Where(n => n.Id == node_id && n.RoadmapId == roadmap_id)
            .Select(n => n.ScheduleBlockId).FirstOrDefaultAsync();
        var r = await ScheduleBlockLogic.RemoveAsync(db, roadmap_id, node_id);
        return r.Outcome == ScheduleBlockLogic.Outcome.Ok && blockId is Guid b
            ? await Answer(r, roadmap_id, b, "removed")
            : J(new { error = r.Error });
    }

    [McpServerTool(Name = "set_pool_items_in_play"), Description(
        "Pool blocks only: put items in play (in_play=true) or out of it (false). Items out of play stay in the pool but " +
        "are left out of its sessions and of the item picker when logging. Only the listed items change.")]
    public async Task<string> SetInPlay(
        [Description("Roadmap UUID")] Guid roadmap_id,
        [Description("Pool block UUID")] Guid block_id,
        [Description("Item UUIDs in this pool")] Guid[] node_ids,
        [Description("true = in play, false = out of play")] bool in_play)
    {
        if (node_ids.Length == 0) return J(new { error = "give at least one item id" });
        var r = await ScheduleBlockLogic.SetActiveAsync(db, roadmap_id, block_id, node_ids, in_play);
        return await Answer(r, roadmap_id, block_id, in_play ? "in play" : "out of play");
    }
}
