using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.DTOs.Common;
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

    /// <inheritdoc />
    public async Task<Result<PagedResult<AuditLogDto>>> GetAsync(int page, int pageSize, string? action = null)
    {
        // Clamp paging to a safe range so a bad client cannot request the whole table.
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var query = _context.AuditLogs.AsNoTracking();

        // Optional exact-match filter on the action code.
        if (!string.IsNullOrWhiteSpace(action))
        {
            var trimmed = action.Trim();
            query = query.Where(a => a.Action == trimmed);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.CreatedAt)       // newest first
            .Skip((page - 1) * pageSize)               // skip earlier pages
            .Take(pageSize)                            // take the current page
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                ActorId = a.ActorId,
                ActorEmail = a.ActorEmail,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Details = a.Details,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync();

        return Result<PagedResult<AuditLogDto>>.Ok(new PagedResult<AuditLogDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
        });
    }
}
