namespace Taskpilot.API.DTOs.Marketplace;

/// <summary>Input for posting a new marketplace task.</summary>
public class CreateTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }

    /// <summary>Comma-separated required skills (optional).</summary>
    public string? RequiredSkills { get; set; }

    /// <summary>Optional deadline (UTC).</summary>
    public DateTime? Deadline { get; set; }
}
