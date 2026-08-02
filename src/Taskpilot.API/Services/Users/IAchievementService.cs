using Taskpilot.API.DTOs.Users;

namespace Taskpilot.API.Services;

/// <summary>Computes a user's achievement badges from their activity and reputation.</summary>
public interface IAchievementService
{
    /// <summary>The full badge set for a user, each with progress and whether it's earned.</summary>
    Task<List<AchievementDto>> GetForUserAsync(Guid userId);
}
