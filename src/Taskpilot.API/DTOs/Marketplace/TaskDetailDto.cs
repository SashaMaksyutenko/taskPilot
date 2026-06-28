namespace Taskpilot.API.DTOs.Marketplace;

/// <summary>A marketplace task with full details and its applications.</summary>
public class TaskDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public string? RequiredSkills { get; set; }
    public DateTime? Deadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid PosterId { get; set; }
    public string PosterName { get; set; } = string.Empty;
    public string? PosterAvatarUrl { get; set; }
    public Guid? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string? AssigneeAvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ApplicationDto> Applications { get; set; } = new();
}
