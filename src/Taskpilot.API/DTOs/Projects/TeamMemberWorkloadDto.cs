using Taskpilot.API.DTOs.Calendar;

namespace Taskpilot.API.DTOs.Projects;

/// <summary>
/// One project participant's workload over a date range: who they are plus the tasks
/// assigned to them in this project that fall due within the range. Used by the team
/// availability view to show who is busy when (and who is free).
/// </summary>
public class TeamMemberWorkloadDto
{
    /// <summary>The participant's user id.</summary>
    public Guid UserId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Avatar URL, or null when the user has no avatar.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>True for the project owner.</summary>
    public bool IsOwner { get; set; }

    /// <summary>
    /// Tasks assigned to this participant in the project with a deadline inside the
    /// requested range, ordered by deadline. Empty when the participant is free.
    /// </summary>
    public List<CalendarTaskDto> Tasks { get; set; } = new();
}
