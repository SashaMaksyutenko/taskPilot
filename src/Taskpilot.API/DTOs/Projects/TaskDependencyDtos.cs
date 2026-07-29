namespace Taskpilot.API.DTOs.Projects;

/// <summary>A light reference to a task, used in dependency listings.</summary>
public class TaskRefDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>Input for adding a dependency: the task this one depends on (is blocked by).</summary>
public class AddDependencyDto
{
    public Guid DependsOnTaskId { get; set; }
}

/// <summary>A task's dependency graph as returned to clients.</summary>
public class TaskDependenciesDto
{
    /// <summary>Tasks this task depends on (its blockers).</summary>
    public List<TaskRefDto> DependsOn { get; set; } = new();

    /// <summary>Tasks that depend on this task (that it blocks).</summary>
    public List<TaskRefDto> Blocks { get; set; } = new();

    /// <summary>True when at least one blocker isn't Done yet.</summary>
    public bool IsBlocked { get; set; }
}
