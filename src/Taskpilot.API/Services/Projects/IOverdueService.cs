namespace Taskpilot.API.Services;

/// <summary>Flags newly-overdue tasks and notifies their owners (once per task).</summary>
public interface IOverdueService
{
    /// <summary>
    /// Finds overdue tasks not yet flagged, notifies the owner, dispatches the
    /// "task.overdue" webhook and marks them flagged. Returns how many were processed.
    /// </summary>
    Task<int> ProcessOverdueAsync();
}
