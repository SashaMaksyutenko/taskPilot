using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Task deadline-extension requests. Anyone with access to the task's project can raise
/// one; only the project owner decides. Approving moves the deadline and resets the
/// task's overdue/escalation flags so it is re-evaluated against the new date.
/// </summary>
public class ExtensionService : IExtensionService
{
    private readonly TaskpilotDbContext _context;
    private readonly INotificationService _notifications;
    private readonly ILogger<ExtensionService> _logger;

    public ExtensionService(
        TaskpilotDbContext context,
        INotificationService notifications,
        ILogger<ExtensionService> logger)
    {
        _context = context;
        _notifications = notifications;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ExtensionRequestDto>> RequestAsync(Guid userId, Guid taskId, CreateExtensionRequestDto dto)
    {
        var task = await _context.ProjectTasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return Result<ExtensionRequestDto>.Fail("Task not found.");

        if (!await ProjectAccess.CanAccessAsync(_context, task.ProjectId, userId))
            return Result<ExtensionRequestDto>.Fail("Task not found.");

        if (dto.RequestedDeadline <= DateTime.UtcNow)
            return Result<ExtensionRequestDto>.Fail("The requested deadline must be in the future.");

        // Only one open request per task at a time.
        var hasPending = await _context.TaskExtensionRequests
            .AnyAsync(r => r.TaskId == taskId && r.Status == ExtensionRequestStatus.Pending);
        if (hasPending)
            return Result<ExtensionRequestDto>.Fail("There is already a pending extension request for this task.");

        var request = new TaskExtensionRequest
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            RequesterId = userId,
            RequestedDeadline = dto.RequestedDeadline,
            Reason = dto.Reason.Trim(),
            Status = ExtensionRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        _context.TaskExtensionRequests.Add(request);
        await _context.SaveChangesAsync();

        // Notify the project owner (unless they raised it themselves).
        if (task.Project.OwnerId != userId)
            await _notifications.CreateAsync(
                task.Project.OwnerId,
                NotificationType.Task,
                $"Extension requested for \"{task.Title}\"",
                $"/projects/{task.ProjectId}");

        _logger.LogInformation("Extension requested. TaskId: {TaskId}, RequesterId: {UserId}", taskId, userId);
        return Result<ExtensionRequestDto>.Ok(await MapAsync(request, userId, task.Project.OwnerId));
    }

    /// <inheritdoc />
    public async Task<Result<ExtensionRequestDto>> DecideAsync(Guid userId, Guid requestId, bool approve)
    {
        var request = await _context.TaskExtensionRequests
            .Include(r => r.Task).ThenInclude(t => t.Project)
            .FirstOrDefaultAsync(r => r.Id == requestId);
        if (request is null)
            return Result<ExtensionRequestDto>.Fail("Request not found.");

        // Only the project owner may decide.
        if (request.Task.Project.OwnerId != userId)
            return Result<ExtensionRequestDto>.Fail("Only the project owner can decide extension requests.");

        if (request.Status != ExtensionRequestStatus.Pending)
            return Result<ExtensionRequestDto>.Fail("This request has already been decided.");

        request.Status = approve ? ExtensionRequestStatus.Approved : ExtensionRequestStatus.Rejected;
        request.DecidedById = userId;
        request.DecidedAt = DateTime.UtcNow;

        if (approve)
        {
            // Move the deadline and let the task be re-evaluated against the new date.
            request.Task.Deadline = request.RequestedDeadline;
            request.Task.OverdueNotifiedAt = null;
            request.Task.EscalatedAt = null;
            request.Task.EscalationLevel = 0;
        }

        await _context.SaveChangesAsync();

        // Tell the requester the outcome.
        await _notifications.CreateAsync(
            request.RequesterId,
            NotificationType.Task,
            approve
                ? $"Your extension for \"{request.Task.Title}\" was approved."
                : $"Your extension for \"{request.Task.Title}\" was rejected.",
            $"/projects/{request.Task.ProjectId}");

        _logger.LogInformation("Extension {Decision}. RequestId: {RequestId}, By: {UserId}",
            approve ? "approved" : "rejected", requestId, userId);
        return Result<ExtensionRequestDto>.Ok(await MapAsync(request, userId, request.Task.Project.OwnerId));
    }

    /// <inheritdoc />
    public async Task<Result<List<ExtensionRequestDto>>> GetForTaskAsync(Guid userId, Guid taskId)
    {
        var task = await _context.ProjectTasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return Result<List<ExtensionRequestDto>>.Fail("Task not found.");

        if (!await ProjectAccess.CanAccessAsync(_context, task.ProjectId, userId))
            return Result<List<ExtensionRequestDto>>.Fail("Task not found.");

        var isOwner = task.Project.OwnerId == userId;
        var requests = await _context.TaskExtensionRequests
            .Where(r => r.TaskId == taskId)
            .Include(r => r.Requester)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var dtos = requests.Select(r => Map(r, r.Requester.Name, isOwner)).ToList();
        return Result<List<ExtensionRequestDto>>.Ok(dtos);
    }

    /// <summary>Maps a request, resolving the requester name from the database.</summary>
    private async Task<ExtensionRequestDto> MapAsync(TaskExtensionRequest request, Guid viewerId, Guid ownerId)
    {
        var requesterName = await _context.Users
            .Where(u => u.Id == request.RequesterId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync() ?? "Unknown";
        return Map(request, requesterName, viewerId == ownerId);
    }

    /// <summary>Shapes a request for the client; the owner may decide pending ones.</summary>
    private static ExtensionRequestDto Map(TaskExtensionRequest r, string requesterName, bool isOwner) => new()
    {
        Id = r.Id,
        TaskId = r.TaskId,
        RequesterId = r.RequesterId,
        RequesterName = requesterName,
        RequestedDeadline = r.RequestedDeadline,
        Reason = r.Reason,
        Status = r.Status.ToString(),
        CreatedAt = r.CreatedAt,
        DecidedAt = r.DecidedAt,
        CanDecide = isOwner && r.Status == ExtensionRequestStatus.Pending,
    };
}
