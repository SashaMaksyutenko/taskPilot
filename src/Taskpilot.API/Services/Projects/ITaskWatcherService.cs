using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>Manages task "watchers" — users subscribed to a task's notifications.</summary>
public interface ITaskWatcherService
{
    /// <summary>Returns a task's watchers and whether the caller is watching it (any project member).</summary>
    Task<Result<TaskWatchersDto>> GetAsync(Guid userId, Guid taskId);

    /// <summary>Subscribes the caller to a task (idempotent). Any project member may watch.</summary>
    Task<Result<TaskWatchersDto>> WatchAsync(Guid userId, Guid taskId);

    /// <summary>Unsubscribes the caller from a task (idempotent).</summary>
    Task<Result<TaskWatchersDto>> UnwatchAsync(Guid userId, Guid taskId);
}
