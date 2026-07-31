namespace Taskpilot.API.DTOs.Projects;

/// <summary>A task assigned to the current user, with its project, for the cross-project "My work" list.</summary>
public class MyTaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }

    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? ProjectColor { get; set; }
}
