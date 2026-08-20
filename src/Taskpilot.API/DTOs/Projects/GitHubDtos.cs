namespace Taskpilot.API.DTOs.Projects;

/// <summary>Body to connect a project to a GitHub repository.</summary>
public class GitHubConnectDto
{
    public string Repo { get; set; } = string.Empty;
}

/// <summary>Whether a project is linked to a GitHub repo (webhook secret is never returned here).</summary>
public class GitHubStatusDto
{
    public bool Connected { get; set; }
    public string? Repo { get; set; }
    public string? WebhookUrl { get; set; }

    /// <summary>What a merged PR that closes a task does to it: "None" | "Review" | "Done".</summary>
    public string MergeAction { get; set; } = "Review";
}

/// <summary>Body to change what a merged PR does to a closed task ("None" | "Review" | "Done").</summary>
public class SetGitHubMergeActionDto
{
    public string MergeAction { get; set; } = string.Empty;
}

/// <summary>Returned once on connect — includes the secret to paste into the GitHub webhook.</summary>
public class GitHubConnectResultDto
{
    public string Repo { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

/// <summary>A commit/PR that references a task, shown on the task.</summary>
public class GitHubTaskLinkDto
{
    public string Kind { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Outcome of processing one webhook delivery.</summary>
public class GitHubWebhookResultDto
{
    public int Linked { get; set; }
    public int Closed { get; set; }
}
