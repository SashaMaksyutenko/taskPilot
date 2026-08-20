namespace Taskpilot.API.DTOs.Integrations;

/// <summary>Whether the GitHub integration is available and linked for the current user.</summary>
public class GitHubConnectionStatusDto
{
    /// <summary>True when the server has the GitHub integration OAuth app configured.</summary>
    public bool Configured { get; set; }

    /// <summary>True when this user has linked their GitHub account.</summary>
    public bool Connected { get; set; }

    /// <summary>The linked GitHub login, when connected.</summary>
    public string? Login { get; set; }

    /// <summary>When the account was linked (UTC), when connected.</summary>
    public DateTime? ConnectedAt { get; set; }
}

/// <summary>A repository the linked GitHub account can access.</summary>
public class GitHubRepoDto
{
    public string FullName { get; set; } = string.Empty;
    public bool Private { get; set; }
}

/// <summary>Body for completing the GitHub link flow (code + the callback used to get it).</summary>
public class GitHubConnectDto
{
    public string Code { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? State { get; set; }
}
