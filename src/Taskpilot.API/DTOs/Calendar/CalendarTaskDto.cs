namespace Taskpilot.API.DTOs.Calendar;

/// <summary>A task shown on the calendar (only tasks with a deadline).</summary>
public class CalendarTaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
}
