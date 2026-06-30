namespace Taskpilot.API.Models;

/// <summary>A collaborator's permission level on a project.</summary>
public enum ProjectMemberRole
{
    /// <summary>Full access: view and edit the board, tasks and comments.</summary>
    Editor = 0,

    /// <summary>Read-only: can view but not create/edit/delete or comment.</summary>
    Viewer = 1
}
