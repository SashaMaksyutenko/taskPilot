namespace Taskpilot.API.Models;

/// <summary>Kanban status of a task: Backlog → In Progress → Review → Done.</summary>
public enum ProjectTaskStatus
{
    Backlog = 0,
    InProgress = 1,
    Review = 2,
    Done = 3
}
