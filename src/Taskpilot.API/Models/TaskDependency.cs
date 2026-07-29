namespace Taskpilot.API.Models;

/// <summary>
/// A dependency between two tasks in the same project: <see cref="TaskId"/> is "blocked by"
/// <see cref="DependsOnTaskId"/> (i.e. the dependent task shouldn't be completed until the
/// task it depends on is done). The graph is kept acyclic — adding a dependency that would
/// create a cycle is rejected by the service.
/// </summary>
public class TaskDependency
{
    public Guid Id { get; set; }

    /// <summary>The dependent (blocked) task.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Navigation to the dependent task.</summary>
    public ProjectTask Task { get; set; } = null!;

    /// <summary>The task it depends on (the blocker).</summary>
    public Guid DependsOnTaskId { get; set; }

    /// <summary>Navigation to the blocker task.</summary>
    public ProjectTask DependsOnTask { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
