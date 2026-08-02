using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Digest;

namespace Taskpilot.API.Services;

/// <summary>Builds a user's weekly activity digest and an optional AI narrative of it.</summary>
public interface IWeeklyDigestService
{
    /// <summary>True when an LLM is configured to write the narrative.</summary>
    bool IsEnabled { get; }

    /// <summary>The week-in-review numbers for a user (no LLM call — cheap).</summary>
    Task<DigestDto> GetWeeklyAsync(Guid userId);

    /// <summary>An AI-written summary of the user's week (empty when no LLM is configured).</summary>
    Task<Result<DigestSummaryDto>> GetSummaryAsync(Guid userId);
}
