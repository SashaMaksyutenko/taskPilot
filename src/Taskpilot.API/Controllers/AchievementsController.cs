using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>A user's achievement badges (shown on their public profile).</summary>
[ApiController]
[Authorize]
public class AchievementsController : BaseApiController
{
    private readonly IAchievementService _achievements;

    public AchievementsController(IAchievementService achievements)
    {
        _achievements = achievements;
    }

    /// <summary>Returns the badge set (earned + progress) for a user.</summary>
    [HttpGet("api/users/{userId:guid}/achievements")]
    public async Task<IActionResult> Get(Guid userId)
    {
        if (CurrentUserId() is null) return Unauthorized();

        return Ok(await _achievements.GetForUserAsync(userId));
    }
}
