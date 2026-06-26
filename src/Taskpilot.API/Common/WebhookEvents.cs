namespace Taskpilot.API.Common;

/// <summary>The webhook event names TaskPilot can emit.</summary>
public static class WebhookEvents
{
    public const string TaskCompleted = "task.completed";
    public const string TaskCreated = "task.created";
    public const string TaskOverdue = "task.overdue";
    public const string ProjectCreated = "project.created";

    /// <summary>All supported events (used for validation).</summary>
    public static readonly string[] All = { TaskCompleted, TaskCreated, TaskOverdue, ProjectCreated };
}
