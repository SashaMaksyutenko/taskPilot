using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Persists audit-trail entries to the database. Append-only: it never updates or
/// deletes existing logs, so the history stays trustworthy.
/// </summary>
public class AuditService : IAuditService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(TaskpilotDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LogAsync(
        string action,
        Guid? actorId = null,
        string? actorEmail = null,
        string? entityType = null,
        string? entityId = null,
        string? details = null,
        string? ipAddress = null)
    {
        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            ActorId = actorId,
            ActorEmail = actorEmail,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow,
        };

        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync();

        // Mirror the audit entry to the application log for live observability.
        _logger.LogInformation(
            "Audit: {Action} by {ActorId} on {EntityType}:{EntityId}",
            action, actorId, entityType, entityId);
    }
}
