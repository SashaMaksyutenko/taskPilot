using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Reviews;

namespace Taskpilot.API.Services;

/// <summary>
/// Peer reviews across the app. Marketplace ratings still flow through the marketplace service;
/// this service adds reviews in other contexts (project collaboration) and reads a user's
/// received reviews across every context for their profile.
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// Leaves a 1–5 star review about a fellow member of a project. Both users must be active
    /// participants (owner or member) of the project, and a user may review each other member at
    /// most once per project.
    /// </summary>
    Task<Result<UserReviewDto>> LeaveProjectReviewAsync(Guid raterId, Guid projectId, LeaveReviewDto dto);

    /// <summary>Every review a user has received, newest first, with each context resolved for display.</summary>
    Task<Result<List<UserReviewDto>>> GetUserReviewsAsync(Guid userId);
}
