using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Records and reads the reputation ledger — a persisted history of point-affecting
/// events. The profile's total score remains derived; this is the history behind it.
/// </summary>
public interface IReputationService
{
    /// <summary>
    /// Appends a ledger entry. When <paramref name="once"/> is true and an entry already
    /// exists for <paramref name="relatedEntityId"/>, the call is a no-op (idempotent).
    /// </summary>
    Task RecordAsync(
        Guid userId,
        int delta,
        ReputationReason reason,
        string description,
        Guid? relatedEntityId = null,
        bool once = false);

    /// <summary>
    /// Records the reputation event for a completed project task: early (+15), on-time
    /// (+10) or late (−10), based on its deadline and completion time. No-op when the
    /// task has no assignee or no deadline. Idempotent per task.
    /// </summary>
    Task RecordTaskCompletionAsync(ProjectTask task);

    /// <summary>Returns the user's ledger history (newest first) and the ledger total.</summary>
    Task<ReputationHistoryDto> GetHistoryAsync(Guid userId, int limit = 50);
}
