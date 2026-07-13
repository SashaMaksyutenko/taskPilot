namespace Taskpilot.API.Common;

/// <summary>The webhook event names TaskPilot can emit.</summary>
public static class WebhookEvents
{
    public const string TaskCompleted = "task.completed";
    public const string TaskCreated = "task.created";
    public const string TaskUpdated = "task.updated";
    public const string TaskOverdue = "task.overdue";
    public const string ProjectCreated = "project.created";
    public const string ProjectArchived = "project.archived";
    public const string MarketplaceTaskCompleted = "marketplace.task.completed";
    public const string MarketplaceTaskPaid = "marketplace.task.paid";
    public const string MarketplaceApplicationAccepted = "marketplace.application.accepted";
    public const string UserBanned = "user.banned";
    public const string UserJoined = "user.joined";
    public const string CommentCreated = "comment.created";
    public const string WarningIssued = "warning.issued";
    public const string AppealResolved = "appeal.resolved";
    public const string MentionTriggered = "mention.triggered";

    /// <summary>All supported events (used for validation).</summary>
    public static readonly string[] All =
    {
        TaskCompleted, TaskCreated, TaskUpdated, TaskOverdue,
        ProjectCreated, ProjectArchived,
        MarketplaceTaskCompleted, MarketplaceTaskPaid, MarketplaceApplicationAccepted,
        UserBanned, UserJoined,
        CommentCreated, WarningIssued, AppealResolved, MentionTriggered,
    };
}
