namespace Taskpilot.API.Models;

/// <summary>The event that fires a project <see cref="AutomationRule"/>.</summary>
public enum AutomationTrigger
{
    /// <summary>A task was created in the project.</summary>
    OnTaskCreated = 0,

    /// <summary>A task's status changed (optionally filtered to a target status).</summary>
    OnTaskStatusChanged = 1,
}
