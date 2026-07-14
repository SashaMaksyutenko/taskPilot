namespace Taskpilot.API.DTOs.Reports;

/// <summary>Body for scheduling a recurring report email.</summary>
public class CreateReportScheduleDto
{
    /// <summary>"Project" or "Team".</summary>
    public string Kind { get; set; } = "Project";

    /// <summary>"Pdf" or "Xlsx".</summary>
    public string Format { get; set; } = "Pdf";

    /// <summary>"Daily", "Weekly" or "Monthly".</summary>
    public string Frequency { get; set; } = "Weekly";
}

/// <summary>A scheduled report as returned to its owner.</summary>
public class ReportScheduleDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public DateTime? LastSentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
