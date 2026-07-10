namespace Taskpilot.API.Models;

/// <summary>Status of a forum report as it moves through moderation.</summary>
public enum ForumReportStatus
{
    /// <summary>Awaiting a moderator's review.</summary>
    Pending,

    /// <summary>Reviewed and acted upon.</summary>
    Resolved,

    /// <summary>Reviewed and no action taken.</summary>
    Dismissed,
}

/// <summary>A user's report of a forum reply to the moderators.</summary>
public class ForumReport
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Reply that was reported (foreign key).</summary>
    public Guid ReplyId { get; set; }

    /// <summary>Navigation to the reported reply.</summary>
    public ForumReply Reply { get; set; } = null!;

    /// <summary>User who filed the report (foreign key).</summary>
    public Guid ReporterId { get; set; }

    /// <summary>Navigation to the reporter.</summary>
    public User Reporter { get; set; } = null!;

    /// <summary>Optional free-text reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Current moderation status.</summary>
    public ForumReportStatus Status { get; set; } = ForumReportStatus.Pending;

    /// <summary>UTC time the report was filed.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Admin who reviewed the report (foreign key), if any.</summary>
    public Guid? ResolvedById { get; set; }

    /// <summary>UTC time the report was reviewed, if any.</summary>
    public DateTime? ResolvedAt { get; set; }
}
