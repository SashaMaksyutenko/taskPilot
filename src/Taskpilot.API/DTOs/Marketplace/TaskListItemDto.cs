namespace Taskpilot.API.DTOs.Marketplace;

/// <summary>A marketplace task as shown in the browse list (no full description).</summary>
public class TaskListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public string? RequiredSkills { get; set; }
    public DateTime? Deadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid PosterId { get; set; }
    public string PosterName { get; set; } = string.Empty;
    public string? PosterAvatarUrl { get; set; }
    public int ApplicationCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
