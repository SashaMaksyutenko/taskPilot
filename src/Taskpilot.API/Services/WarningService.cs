using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles moderation warnings and escalation. Issuing a warning notifies the user,
/// records an audit entry, and auto-bans the account once it reaches the threshold.
/// </summary>
public class WarningService : IWarningService
{
    /// <summary>Number of warnings that triggers an automatic ban.</summary>
    private const int AutoBanThreshold = 3;

    private readonly TaskpilotDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IAdminService _admin;
    private readonly IWebhookService _webhooks;
    private readonly IReputationService _reputation;
    private readonly ILogger<WarningService> _logger;

    public WarningService(
        TaskpilotDbContext context,
        INotificationService notifications,
        IAuditService audit,
        IAdminService admin,
        IWebhookService webhooks,
        IReputationService reputation,
        ILogger<WarningService> logger)
    {
        _context = context;
        _notifications = notifications;
        _audit = audit;
        _admin = admin;
        _webhooks = webhooks;
        _reputation = reputation;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IssueWarningResultDto>> IssueAsync(
        Guid adminId, string? adminEmail, Guid targetUserId, IssueWarningDto dto, string? ip)
    {
        // An admin must not warn themselves.
        if (adminId == targetUserId)
            return Result<IssueWarningResultDto>.Fail("You cannot warn yourself.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user is null)
            return Result<IssueWarningResultDto>.Fail("User not found.");

        var reason = dto.Reason.Trim();
        var warning = new UserWarning
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            IssuedById = adminId,
            Reason = reason,
            CreatedAt = DateTime.UtcNow,
        };
        _context.UserWarnings.Add(warning);
        await _context.SaveChangesAsync();

        var count = await _context.UserWarnings.CountAsync(w => w.UserId == targetUserId);

        // Tell the user they were warned.
        await _notifications.CreateAsync(
            targetUserId,
            NotificationType.Moderation,
            $"You received a warning: {reason}",
            "/settings");

        await _audit.LogAsync(
            action: "moderation.warning.issued",
            actorId: adminId,
            actorEmail: adminEmail,
            entityType: nameof(User),
            entityId: targetUserId.ToString(),
            details: $"Warning {count}/{AutoBanThreshold}: {reason}",
            ipAddress: ip);

        await _webhooks.DispatchAsync(WebhookEvents.WarningIssued, new
        {
            warningId = warning.Id,
            userId = targetUserId,
            reason,
            count,
        });

        // Deduct reputation for the warning (recorded in the ledger history).
        await _reputation.RecordAsync(targetUserId, -5, ReputationReason.WarningIssued, reason, warning.Id);

        // Escalate: auto-ban once the user reaches the threshold (if still active).
        var autoBanned = false;
        if (count >= AutoBanThreshold && user.IsActive)
        {
            var ban = await _admin.SetActiveAsync(adminId, targetUserId, isActive: false);
            if (ban.Succeeded)
            {
                autoBanned = true;
                await _notifications.CreateAsync(
                    targetUserId,
                    NotificationType.Moderation,
                    $"Your account was suspended after reaching {AutoBanThreshold} warnings.",
                    "/settings");

                await _audit.LogAsync(
                    action: "moderation.autoban",
                    actorId: adminId,
                    actorEmail: adminEmail,
                    entityType: nameof(User),
                    entityId: targetUserId.ToString(),
                    details: $"Auto-banned after {count} warnings.",
                    ipAddress: ip);

                _logger.LogWarning("User auto-banned after warnings. UserId: {UserId}, Count: {Count}", targetUserId, count);
            }
        }

        _logger.LogInformation("Warning issued. UserId: {UserId}, Count: {Count}", targetUserId, count);

        return Result<IssueWarningResultDto>.Ok(new IssueWarningResultDto
        {
            Warning = await ToDtoAsync(warning.Id),
            WarningCount = count,
            AutoBanned = autoBanned,
        });
    }

    /// <inheritdoc />
    public async Task<Result<List<WarningDto>>> GetForUserAsync(Guid userId)
    {
        var warnings = await _context.UserWarnings
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WarningDto
            {
                Id = w.Id,
                UserId = w.UserId,
                Reason = w.Reason,
                IssuedByName = w.IssuedBy.Name,
                CreatedAt = w.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        return Result<List<WarningDto>>.Ok(warnings);
    }

    private Task<WarningDto> ToDtoAsync(Guid warningId) =>
        _context.UserWarnings
            .Where(w => w.Id == warningId)
            .Select(w => new WarningDto
            {
                Id = w.Id,
                UserId = w.UserId,
                Reason = w.Reason,
                IssuedByName = w.IssuedBy.Name,
                CreatedAt = w.CreatedAt,
            })
            .AsNoTracking()
            .FirstAsync();
}
