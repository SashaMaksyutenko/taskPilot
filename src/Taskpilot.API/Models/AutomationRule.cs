namespace Taskpilot.API.Models;

/// <summary>
/// A project automation ("robot"): when <see cref="Trigger"/> fires (optionally only for a
/// specific <see cref="TriggerStatus"/>), the rule runs <see cref="Action"/> on the task.
/// Rules belong to a project and are managed by its owner.
/// </summary>
public class AutomationRule
{
    public Guid Id { get; set; }

    /// <summary>The project this rule belongs to.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Navigation to the project.</summary>
    public Project Project { get; set; } = null!;

    /// <summary>A short human-readable label for the rule.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the rule is active. Disabled rules never fire.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>The event that fires the rule.</summary>
    public AutomationTrigger Trigger { get; set; }

    /// <summary>
    /// For <see cref="AutomationTrigger.OnTaskStatusChanged"/>, the status a task must be moved to
    /// for the rule to fire. Null means "any status change".
    /// </summary>
    public ProjectTaskStatus? TriggerStatus { get; set; }

    /// <summary>What the rule does when it fires.</summary>
    public AutomationAction Action { get; set; }

    /// <summary>The action's parameter (priority name, assignee id or comment text); null for NotifyOwner.</summary>
    public string? ActionValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
