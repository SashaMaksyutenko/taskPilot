namespace Taskpilot.API.Models;

/// <summary>Which report a schedule generates. Stored as a string.</summary>
public enum ReportKind
{
    /// <summary>Project health: tasks by status, completion, overdue, per-member.</summary>
    Project,

    /// <summary>Team performance: completion rate, on-time rate, overdue, reputation.</summary>
    Team,
}

/// <summary>File format a scheduled report is delivered in. Stored as a string.</summary>
public enum ReportFormat
{
    Pdf,
    Xlsx,
}

/// <summary>How often a scheduled report is emailed. Stored as a string.</summary>
public enum ReportFrequency
{
    Daily,
    Weekly,
    Monthly,
}

/// <summary>
/// A standing request to email a project report to a user on a cadence. The background
/// worker generates the report and sends it whenever the cadence has elapsed.
/// </summary>
public class ReportSchedule
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>User the report is emailed to (foreign key).</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the recipient.</summary>
    public User User { get; set; } = null!;

    /// <summary>Project the report covers (foreign key).</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Navigation to the project.</summary>
    public Project Project { get; set; } = null!;

    /// <summary>Which report to generate.</summary>
    public ReportKind Kind { get; set; }

    /// <summary>Format to attach.</summary>
    public ReportFormat Format { get; set; }

    /// <summary>How often to send it.</summary>
    public ReportFrequency Frequency { get; set; }

    /// <summary>UTC time the report was last emailed; null until the first send.</summary>
    public DateTime? LastSentAt { get; set; }

    /// <summary>UTC time the schedule was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
