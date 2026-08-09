using Taskpilot.API.DTOs.Planning;

namespace Taskpilot.API.Services;

/// <summary>
/// Builds a "what to do next" plan from the user's open, assigned tasks: a deterministic ordering
/// that the LLM re-ranks with a short reason each when it's configured.
/// </summary>
public interface INextActionService
{
    /// <summary>True when the LLM is configured (the plan then carries AI reasons).</summary>
    bool IsEnabled { get; }

    /// <summary>Returns up to <paramref name="limit"/> tasks to focus on, best first.</summary>
    Task<NextActionsDto> GetPlanAsync(Guid userId, int limit = 8);
}
