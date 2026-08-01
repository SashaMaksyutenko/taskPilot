using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>The community leaderboard, ranked by reputation.</summary>
[ApiController]
[Authorize]
[Route("api/leaderboard")]
public class LeaderboardController : BaseApiController
{
    private readonly ILeaderboardService _leaderboard;

    public LeaderboardController(ILeaderboardService leaderboard)
    {
        _leaderboard = leaderboard;
    }

    /// <summary>Top users by reputation plus the caller's own standing.</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int limit = 20)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _leaderboard.GetAsync(userId.Value, limit));
    }
}
