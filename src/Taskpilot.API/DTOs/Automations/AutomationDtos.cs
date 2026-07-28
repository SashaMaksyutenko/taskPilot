namespace Taskpilot.API.DTOs.Automations;

/// <summary>Input for creating or updating a project automation rule.</summary>
public class SaveAutomationRuleDto
{
    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    /// <summary>"OnTaskCreated" | "OnTaskStatusChanged".</summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>For OnTaskStatusChanged: "Backlog"/"InProgress"/"Review"/"Done", or null for any.</summary>
    public string? TriggerStatus { get; set; }

    /// <summary>"SetPriority" | "AssignToUser" | "NotifyOwner" | "AddComment".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The action parameter (priority name, assignee id or comment text); null for NotifyOwner.</summary>
    public string? ActionValue { get; set; }
}

/// <summary>A project automation rule as returned to clients.</summary>
public class AutomationRuleDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public string? TriggerStatus { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? ActionValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
