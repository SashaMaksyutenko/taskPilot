namespace Taskpilot.API.DTOs.Projects;

/// <summary>Input for creating or updating a project (same fields for both).</summary>
public class SaveProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional UI color tag (e.g. hex like "#4F46E5").</summary>
    public string? Color { get; set; }
}
