namespace Taskpilot.API.DTOs.Projects;

/// <summary>Body for raising a deadline-extension request.</summary>
public class CreateExtensionRequestDto
{
    /// <summary>The new deadline being requested (UTC).</summary>
    public DateTime RequestedDeadline { get; set; }

    /// <summary>Why the extension is needed.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Body for approving or rejecting an extension request.</summary>
public class DecideExtensionDto
{
    /// <summary>True to approve (moves the deadline), false to reject.</summary>
    public bool Approve { get; set; }
}

/// <summary>An extension request shaped for the client.</summary>
public class ExtensionRequestDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public DateTime RequestedDeadline { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }

    /// <summary>True when the current viewer (project owner) may decide this pending request.</summary>
    public bool CanDecide { get; set; }
}
