using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Writes and reads the reputation ledger. Records are immutable, point-in-time facts;
/// the displayed profile score stays derived elsewhere, so this ledger never has to
/// reconcile mutable aggregates (net votes, average stars).
/// </summary>
public class ReputationService : IReputationService
{
    // Points awarded/deducted for a task's timeliness (mirrors the derived calculation).
    private const int EarlyBonus = 15;
    private const int OnTimeBonus = 10;
    private const int LatePenalty = 10;

    private readonly TaskpilotDbContext _context;
    private readonly ILogger<ReputationService> _logger;

    public ReputationService(TaskpilotDbContext context, ILogger<ReputationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordAsync(
        Guid userId,
        int delta,
        ReputationReason reason,
        string description,
        Guid? relatedEntityId = null,
        bool once = false)
    {
        // Idempotency: skip if we already logged this source event.
        if (once && relatedEntityId is { } id
            && await _context.ReputationEntries.AnyAsync(e => e.RelatedEntityId == id))
        {
            return;
        }

        _context.ReputationEntries.Add(new ReputationEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Delta = delta,
            Reason = reason,
            Description = description,
            RelatedEntityId = relatedEntityId,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reputation entry recorded. UserId: {UserId}, Delta: {Delta}, Reason: {Reason}",
            userId, delta, reason);
    }

    /// <inheritdoc />
    public async Task RecordTaskCompletionAsync(ProjectTask task)
    {
        // Only assigned tasks with a deadline count toward reputation.
        if (task.AssigneeId is not { } assigneeId || task.Deadline is not { } deadline)
            return;

        var completedAt = task.CompletedAt ?? DateTime.UtcNow;

        (int delta, ReputationReason reason) = completedAt > deadline
            ? (-LatePenalty, ReputationReason.TaskLate)
            : (deadline - completedAt).TotalDays >= 1
                ? (EarlyBonus, ReputationReason.TaskEarly)
                : (OnTimeBonus, ReputationReason.TaskOnTime);

        await RecordAsync(assigneeId, delta, reason, task.Title, task.Id, once: true);
    }

    /// <inheritdoc />
    public async Task<ReputationHistoryDto> GetHistoryAsync(Guid userId, int limit = 50)
    {
        // Clamp the page size so a caller can't ask for an unbounded list.
        if (limit is < 1 or > 200) limit = 50;

        var entries = await _context.ReputationEntries
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .Select(e => new ReputationEntryDto
            {
                Id = e.Id,
                Delta = e.Delta,
                Reason = e.Reason.ToString(),
                Description = e.Description,
                CreatedAt = e.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        // Running total across the whole ledger (not just the returned page).
        var ledgerTotal = await _context.ReputationEntries
            .Where(e => e.UserId == userId)
            .SumAsync(e => (int?)e.Delta) ?? 0;

        return new ReputationHistoryDto { Entries = entries, LedgerTotal = ledgerTotal };
    }
}
