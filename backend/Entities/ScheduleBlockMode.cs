namespace Roadmap.Api.Entities;

/// <summary>
/// How a schedule block turns its items into scheduled sessions.
/// </summary>
public enum ScheduleBlockMode
{
    /// <summary>
    /// Items queue behind each other in <c>BlockSortOrder</c>: the head item owns every session
    /// until it is finished, then the next one takes over. What is scheduled is always an item.
    /// </summary>
    Queue = 0,

    /// <summary>
    /// The block itself owns the session; its items are interchangeable candidates. Nothing
    /// decides in advance which one you will work — the plan is drawn from the *average* rate
    /// across the block's active items, frozen at Start Sprint, and you pick the exact item when
    /// you log. Order is meaningless here; the active flag is what decides membership.
    /// </summary>
    Pool = 1
}
