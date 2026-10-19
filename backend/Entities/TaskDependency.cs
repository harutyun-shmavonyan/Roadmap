namespace Roadmap.Api.Entities;

/// <summary>
/// One edge of the task graph: <see cref="Task"/> depends on <see cref="DependsOnTask"/>, i.e. it
/// cannot sensibly be started until the other one is done. <c>TaskLogic</c> keeps the graph acyclic
/// and inside one roadmap. A task with an unfinished prerequisite is <em>blocked</em>: the tab says
/// so and the day's schedule leaves it out, but completing it anyway is allowed — the dependency is
/// advice about order, not a lock.
/// </summary>
public class TaskDependency
{
    /// <summary>The task that waits.</summary>
    public Guid TaskId { get; set; }

    /// <summary>The task it waits on.</summary>
    public Guid DependsOnTaskId { get; set; }

    // Navigation
    public SingleTask Task { get; set; } = null!;
    public SingleTask DependsOnTask { get; set; } = null!;
}
