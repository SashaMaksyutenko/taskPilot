namespace Taskpilot.API.Models;

/// <summary>
/// How a task repeats. When a recurring task is completed, the next occurrence is created
/// automatically with its deadline advanced by the interval (see the task service).
/// </summary>
public enum RecurrenceType
{
    /// <summary>Not a recurring task (the default).</summary>
    None = 0,

    /// <summary>Repeats every N days.</summary>
    Daily = 1,

    /// <summary>Repeats every N weeks.</summary>
    Weekly = 2,

    /// <summary>Repeats every N months.</summary>
    Monthly = 3,
}
