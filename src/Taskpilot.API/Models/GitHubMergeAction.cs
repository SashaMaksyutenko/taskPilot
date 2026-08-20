namespace Taskpilot.API.Models;

/// <summary>
/// What a merged pull request (that closes a task with a keyword) does to that task's status.
/// Lets each project respect its own workflow instead of a hardcoded "always Done".
/// </summary>
public enum GitHubMergeAction
{
    /// <summary>Only link the PR to the task; never change its status.</summary>
    None = 0,

    /// <summary>Move the task to Review so a human still makes the final Done call (default).</summary>
    Review = 1,

    /// <summary>Move the task straight to Done (fully automated).</summary>
    Done = 2,
}
