namespace Taskpilot.API.DTOs.Projects;

/// <summary>One user watching a task.</summary>
public class TaskWatcherDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

/// <summary>A task's watchers plus whether the current user is among them.</summary>
public class TaskWatchersDto
{
    public List<TaskWatcherDto> Watchers { get; set; } = new();

    /// <summary>True when the requesting user is watching the task.</summary>
    public bool IsWatching { get; set; }
}
