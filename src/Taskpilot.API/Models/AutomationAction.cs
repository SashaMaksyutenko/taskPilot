namespace Taskpilot.API.Models;

/// <summary>What a project <see cref="AutomationRule"/> does when its trigger fires.</summary>
public enum AutomationAction
{
    /// <summary>Sets the task's priority (ActionValue = "Low"/"Medium"/"High").</summary>
    SetPriority = 0,

    /// <summary>Assigns the task to a user (ActionValue = the user id, who must be a project member).</summary>
    AssignToUser = 1,

    /// <summary>Notifies the project owner (no ActionValue).</summary>
    NotifyOwner = 2,

    /// <summary>Adds a comment to the task, authored by the project owner (ActionValue = the comment text).</summary>
    AddComment = 3,
}
