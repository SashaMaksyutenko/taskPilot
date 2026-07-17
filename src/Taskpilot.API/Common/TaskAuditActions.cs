namespace Taskpilot.API.Common;

/// <summary>
/// The audit-trail action codes recorded for a task's history. Kept as constants
/// (like <see cref="WebhookEvents"/>) because the same strings are written by the
/// service and rendered by the client, so a typo must not silently split a history.
/// </summary>
public static class TaskAuditActions
{
    public const string Created = "task.created";
    public const string Updated = "task.updated";
    public const string StatusChanged = "task.status.changed";
    public const string Rescheduled = "task.rescheduled";
    public const string Moved = "task.moved";
    public const string Deleted = "task.deleted";
}
