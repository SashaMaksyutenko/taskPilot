namespace Taskpilot.API.Models;

/// <summary>
/// Why a reputation ledger entry was recorded. Stored as a string in the database
/// so the history stays readable and stable across enum reordering.
/// </summary>
public enum ReputationReason
{
    /// <summary>A task was finished a full day or more before its deadline.</summary>
    TaskEarly,

    /// <summary>A task was finished on or before its deadline (less than a day early).</summary>
    TaskOnTime,

    /// <summary>A task was finished after its deadline.</summary>
    TaskLate,

    /// <summary>A marketplace task the user delivered was approved by the poster.</summary>
    MarketplaceCompleted,

    /// <summary>A forum reply by the user was accepted as the solution.</summary>
    ForumSolution,

    /// <summary>The user received a moderation warning.</summary>
    WarningIssued,
}
