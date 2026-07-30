namespace Taskpilot.API.Models;

/// <summary>Lifecycle of a sprint/iteration.</summary>
public enum SprintStatus
{
    /// <summary>Being planned; tasks are being added (the default).</summary>
    Planned = 0,

    /// <summary>Currently in progress.</summary>
    Active = 1,

    /// <summary>Finished — kept for velocity/history.</summary>
    Completed = 2,
}
