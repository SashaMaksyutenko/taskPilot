using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Reviews;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Peer reviews outside the marketplace (project collaboration) and reading a user's reviews.</summary>
[ApiController]
[Authorize]
[Route("api/reviews")]
public class ReviewsController : BaseApiController
{
    private readonly IReviewService _reviews;

    public ReviewsController(IReviewService reviews)
    {
        _reviews = reviews;
    }

    /// <summary>Leaves a review about a fellow member of a project.</summary>
    [HttpPost("project/{projectId:guid}")]
    public async Task<IActionResult> LeaveProjectReview(Guid projectId, [FromBody] LeaveReviewDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _reviews.LeaveProjectReviewAsync(userId.Value, projectId, dto);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>All reviews a user has received, across every context.</summary>
    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetUserReviews(Guid userId)
    {
        var result = await _reviews.GetUserReviewsAsync(userId);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
