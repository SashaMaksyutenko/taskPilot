namespace Taskpilot.API.DTOs.Projects;

/// <summary>Payload to import tasks from CSV text into a project.</summary>
public class ImportTasksDto
{
    /// <summary>
    /// CSV with a header row. Recognized columns (case-insensitive, any order): Title (required),
    /// Status, Priority, Deadline, Assignee, Description. Matches the board's CSV export.
    /// </summary>
    public string Csv { get; set; } = string.Empty;
}

/// <summary>Outcome of a CSV import.</summary>
public class ImportResultDto
{
    /// <summary>Number of tasks created.</summary>
    public int Created { get; set; }

    /// <summary>Number of rows skipped (invalid).</summary>
    public int Skipped { get; set; }

    /// <summary>Per-row problems (capped), e.g. "Row 3: title is required".</summary>
    public List<string> Errors { get; set; } = new();
}
