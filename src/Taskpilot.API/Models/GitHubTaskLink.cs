namespace Taskpilot.API.Models;

/// <summary>
/// A commit or pull request (from the project's linked GitHub repo) that references a task.
/// Created/updated by the inbound webhook when a commit message or PR mentions the task's id.
/// </summary>
public class GitHubTaskLink
{
    public Guid Id { get; set; }

    /// <summary>The referenced task (foreign key).</summary>
    public Guid TaskId { get; set; }

    /// <summary>Navigation to the task.</summary>
    public ProjectTask Task { get; set; } = null!;

    /// <summary>"Commit" or "PullRequest".</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The commit SHA or the pull-request number.</summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>Commit message (first line) or PR title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Link to the commit/PR on GitHub.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Current state, e.g. "pushed" (commit) or "open"/"closed"/"merged" (PR).</summary>
    public string State { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
