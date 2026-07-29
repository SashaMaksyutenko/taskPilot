namespace Taskpilot.API.DTOs.Projects;

/// <summary>The current share state of a project board.</summary>
public class ShareLinkDto
{
    /// <summary>The opaque share token, or null when the board isn't shared.</summary>
    public string? Token { get; set; }

    /// <summary>True when the board is currently shared publicly.</summary>
    public bool Enabled { get; set; }
}

/// <summary>A task as shown on a public (read-only, no-login) board — no ids or private data.</summary>
public class PublicTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? AssigneeName { get; set; }
    public DateTime? Deadline { get; set; }
    public List<string> Tags { get; set; } = new();
}

/// <summary>A project board rendered for anonymous viewers via a share token.</summary>
public class PublicBoardDto
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public List<PublicTaskDto> Tasks { get; set; } = new();
}
