using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles moderation appeals: users file them against their warnings and admins
/// resolve them. Approving an appeal removes the linked warning.
/// </summary>
public class AppealService : IAppealService
{
    private readonly TaskpilotDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly ILogger<AppealService> _logger;

    public AppealService(
        TaskpilotDbContext context,
        INotificationService notifications,
        IAuditService audit,
        ILogger<AppealService> logger)
    {
        _context = context;
        _notifications = notifications;
        _audit = audit;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<AppealDto>> CreateAsync(Guid userId, CreateAppealDto dto)
    {
        // If a warning is referenced, it must belong to the caller.
        if (dto.WarningId is { } warningId)
        {
            var owns = await _context.UserWarnings.AnyAsync(w => w.Id == warningId && w.UserId == userId);
            if (!owns)
                return Result<AppealDto>.Fail("Warning not found.");

            // Prevent duplicate pending appeals for the same warning.
            var dup = await _context.Appeals.AnyAsync(a =>
                a.WarningId == warningId && a.Status == AppealStatus.Pending);
            if (dup)
                return Result<AppealDto>.Fail("An appeal for this warning is already pending.");
        }

        var appeal = new Appeal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WarningId = dto.WarningId,
            Message = dto.Message.Trim(),
            Status = AppealStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        _context.Appeals.Add(appeal);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Appeal filed. AppealId: {AppealId}, UserId: {UserId}", appeal.Id, userId);
        return Result<AppealDto>.Ok(await LoadDtoAsync(appeal.Id));
    }

    /// <inheritdoc />
    public async Task<Result<List<AppealDto>>> GetMineAsync(Guid userId)
    {
        var rows = await _context.Appeals
            .Where(a => a.UserId == userId)
            .Include(a => a.User)
            .Include(a => a.Warning)
            .OrderByDescending(a => a.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<AppealDto>>.Ok(rows.Select(MapDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<List<AppealDto>>> GetAllAsync(string? status = null)
    {
        var query = _context.Appeals
            .Include(a => a.User)
            .Include(a => a.Warning)
            .AsQueryable();

        // Optional status filter (e.g. "Pending").
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppealStatus>(status, ignoreCase: true, out var parsed))
            query = query.Where(a => a.Status == parsed);

        var rows = await query
            // Pending first, then newest.
            .OrderBy(a => a.Status)
            .ThenByDescending(a => a.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<AppealDto>>.Ok(rows.Select(MapDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<AppealDto>> ResolveAsync(
        Guid adminId, string? adminEmail, Guid appealId, ResolveAppealDto dto, string? ip)
    {
        var appeal = await _context.Appeals
            .Include(a => a.Warning)
            .FirstOrDefaultAsync(a => a.Id == appealId);
        if (appeal is null)
            return Result<AppealDto>.Fail("Appeal not found.");

        if (appeal.Status != AppealStatus.Pending)
            return Result<AppealDto>.Fail("This appeal has already been resolved.");

        appeal.Status = dto.Approve ? AppealStatus.Approved : AppealStatus.Rejected;
        appeal.ReviewedById = adminId;
        appeal.ReviewNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        appeal.ReviewedAt = DateTime.UtcNow;

        // Approving lifts the warning (the FK is set null by the cascade).
        if (dto.Approve && appeal.Warning is not null)
            _context.UserWarnings.Remove(appeal.Warning);

        await _context.SaveChangesAsync();

        // Tell the user the outcome.
        await _notifications.CreateAsync(
            appeal.UserId,
            NotificationType.Moderation,
            dto.Approve ? "Your appeal was approved and the warning was lifted." : "Your appeal was rejected.",
            "/settings");

        await _audit.LogAsync(
            action: dto.Approve ? "moderation.appeal.approved" : "moderation.appeal.rejected",
            actorId: adminId,
            actorEmail: adminEmail,
            entityType: nameof(Appeal),
            entityId: appealId.ToString(),
            details: appeal.ReviewNote,
            ipAddress: ip);

        _logger.LogInformation("Appeal resolved. AppealId: {AppealId}, Approved: {Approved}", appealId, dto.Approve);
        return Result<AppealDto>.Ok(await LoadDtoAsync(appealId));
    }

    private async Task<AppealDto> LoadDtoAsync(Guid appealId)
    {
        var appeal = await _context.Appeals
            .Include(a => a.User)
            .Include(a => a.Warning)
            .AsNoTracking()
            .FirstAsync(a => a.Id == appealId);
        return MapDto(appeal);
    }

    private static AppealDto MapDto(Appeal a) => new()
    {
        Id = a.Id,
        UserId = a.UserId,
        UserName = a.User?.Name ?? string.Empty,
        WarningId = a.WarningId,
        WarningReason = a.Warning?.Reason,
        Message = a.Message,
        Status = a.Status.ToString(),
        ReviewNote = a.ReviewNote,
        CreatedAt = a.CreatedAt,
        ReviewedAt = a.ReviewedAt,
    };
}
