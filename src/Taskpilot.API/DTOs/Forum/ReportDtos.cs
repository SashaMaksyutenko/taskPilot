namespace Taskpilot.API.DTOs.Forum;

/// <summary>Input for reporting a reply (the reply id is taken from the route).</summary>
public class CreateReportDto
{
    /// <summary>Optional free-text reason for the report.</summary>
    public string? Reason { get; set; }
}

/// <summary>A forum report as shown in the moderation queue.</summary>
public class ForumReportDto
{
    public Guid Id { get; set; }
    public Guid ReplyId { get; set; }
    public Guid TopicId { get; set; }
    public string TopicTitle { get; set; } = string.Empty;

    /// <summary>Short excerpt of the reported reply's text.</summary>
    public string ReplyExcerpt { get; set; } = string.Empty;
    public string ReplyAuthorName { get; set; } = string.Empty;

    public Guid ReporterId { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Payload to resolve a report: dismiss it, or mark it resolved (acted upon).</summary>
public class ResolveReportDto
{
    public bool Dismiss { get; set; }
}
