using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Calendar;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>
/// Business logic for project tasks: CRUD, Kanban status changes, assignment.
/// Access is scoped through the parent project's owner.
/// </summary>
public interface ITaskService
{
    Task<Result<TaskDto>> CreateTaskAsync(Guid userId, Guid projectId, CreateTaskDto dto);

    /// <summary>Lists a project's tasks, optionally filtered by status (a Kanban column).</summary>
    Task<Result<List<TaskDto>>> GetTasksAsync(Guid userId, Guid projectId, string? status);

    Task<Result<TaskDto>> GetTaskAsync(Guid userId, Guid taskId);

    /// <summary>Lists the subtasks (children) of a task the user can access.</summary>
    Task<Result<List<TaskDto>>> GetSubtasksAsync(Guid userId, Guid taskId);

    Task<Result<TaskDto>> UpdateTaskAsync(Guid userId, Guid taskId, UpdateTaskDto dto);

    /// <summary>Moves a task to another status; sets/clears CompletedAt for Done.</summary>
    Task<Result<TaskDto>> ChangeStatusAsync(Guid userId, Guid taskId, string status);

    /// <summary>
    /// Moves only a task's deadline (e.g. dragged to another day in the calendar),
    /// leaving every other field untouched. Clears the task's overdue/escalation flags
    /// so it is re-evaluated against the new date.
    /// </summary>
    Task<Result<TaskDto>> RescheduleAsync(Guid userId, Guid taskId, DateTime? deadline);

    /// <summary>Changes the status of several tasks at once; returns how many were updated.</summary>
    Task<Result<int>> BulkChangeStatusAsync(Guid userId, IEnumerable<Guid> taskIds, string status);

    /// <summary>Deletes several tasks at once; returns how many were deleted.</summary>
    Task<Result<int>> BulkDeleteAsync(Guid userId, IEnumerable<Guid> taskIds);

    /// <summary>Creates a copy of an existing task in the same project (status reset to Backlog).</summary>
    Task<Result<TaskDto>> DuplicateTaskAsync(Guid userId, Guid taskId);

    /// <summary>Moves a task (and its subtasks) to another project the user can write to.</summary>
    Task<Result<TaskDto>> MoveTaskAsync(Guid userId, Guid taskId, Guid targetProjectId);

    /// <summary>Starts the task's time tracker (no-op if already running).</summary>
    Task<Result<TaskDto>> StartTimerAsync(Guid userId, Guid taskId);

    /// <summary>Stops the task's time tracker and adds the elapsed time.</summary>
    Task<Result<TaskDto>> StopTimerAsync(Guid userId, Guid taskId);

    Task<Result> DeleteTaskAsync(Guid userId, Guid taskId);

    /// <summary>
    /// Returns the task's history (audit trail) newest first: who created, edited, moved,
    /// rescheduled or deleted it and what changed. Readable by anyone with access to the
    /// task's project, so it omits the actor's email and IP.
    /// </summary>
    Task<Result<List<TaskHistoryEntryDto>>> GetHistoryAsync(Guid userId, Guid taskId);

    /// <summary>
    /// Returns the user's tasks (across all their projects) that have a deadline
    /// within the [from, to] range — used to render the calendar.
    /// </summary>
    Task<Result<List<CalendarTaskDto>>> GetCalendarTasksAsync(Guid userId, DateTime from, DateTime to);

    /// <summary>Returns the caller's overdue tasks (past deadline, not Done).</summary>
    Task<Result<List<CalendarTaskDto>>> GetOverdueTasksAsync(Guid userId);

    /// <summary>
    /// Returns each participant of a project (owner + members) with the tasks assigned to
    /// them that fall due within [from, to] — the team's availability. Readable by any
    /// participant of the project.
    /// </summary>
    Task<Result<List<TeamMemberWorkloadDto>>> GetProjectTeamWorkloadAsync(Guid userId, Guid projectId, DateTime from, DateTime to);

    /// <summary>
    /// Exports a project's tasks as a CSV document (the caller must own the project).
    /// </summary>
    Task<Result<string>> ExportTasksCsvAsync(Guid userId, Guid projectId);

    /// <summary>
    /// Exports a project's tasks as an Excel (.xlsx) workbook (caller must own the project).
    /// </summary>
    Task<Result<byte[]>> ExportTasksXlsxAsync(Guid userId, Guid projectId);

    /// <summary>
    /// Exports a project's tasks as a PDF document (caller must own the project).
    /// </summary>
    Task<Result<byte[]>> ExportTasksPdfAsync(Guid userId, Guid projectId);
}
