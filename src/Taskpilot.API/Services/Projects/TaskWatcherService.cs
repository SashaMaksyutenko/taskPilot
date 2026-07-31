using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class TaskWatcherService : ITaskWatcherService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<TaskWatcherService> _logger;

    public TaskWatcherService(TaskpilotDbContext context, ILogger<TaskWatcherService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<TaskWatchersDto>> GetAsync(Guid userId, Guid taskId)
    {
        var projectId = await ProjectIdOfTaskAsync(taskId);
        if (projectId is null || !await ProjectAccess.CanAccessAsync(_context, projectId.Value, userId))
            return Result<TaskWatchersDto>.Fail("Task not found.");

        return Result<TaskWatchersDto>.Ok(await BuildAsync(taskId, userId));
    }

    /// <inheritdoc />
    public async Task<Result<TaskWatchersDto>> WatchAsync(Guid userId, Guid taskId)
    {
        var projectId = await ProjectIdOfTaskAsync(taskId);
        if (projectId is null || !await ProjectAccess.CanAccessAsync(_context, projectId.Value, userId))
            return Result<TaskWatchersDto>.Fail("Task not found.");

        // Idempotent: watching an already-watched task is a no-op.
        if (!await _context.TaskWatchers.AnyAsync(w => w.TaskId == taskId && w.UserId == userId))
        {
            _context.TaskWatchers.Add(new TaskWatcher
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {User} is now watching task {Task}.", userId, taskId);
        }

        return Result<TaskWatchersDto>.Ok(await BuildAsync(taskId, userId));
    }

    /// <inheritdoc />
    public async Task<Result<TaskWatchersDto>> UnwatchAsync(Guid userId, Guid taskId)
    {
        var projectId = await ProjectIdOfTaskAsync(taskId);
        if (projectId is null || !await ProjectAccess.CanAccessAsync(_context, projectId.Value, userId))
            return Result<TaskWatchersDto>.Fail("Task not found.");

        var watch = await _context.TaskWatchers.FirstOrDefaultAsync(w => w.TaskId == taskId && w.UserId == userId);
        if (watch is not null)
        {
            _context.TaskWatchers.Remove(watch);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {User} stopped watching task {Task}.", userId, taskId);
        }

        return Result<TaskWatchersDto>.Ok(await BuildAsync(taskId, userId));
    }

    /// <summary>Loads the current watcher list (name/avatar) and whether the caller is one of them.</summary>
    private async Task<TaskWatchersDto> BuildAsync(Guid taskId, Guid userId)
    {
        // Project to the raw avatar file id (translatable), then build the URL in memory.
        var rows = await _context.TaskWatchers
            .Where(w => w.TaskId == taskId)
            .OrderBy(w => w.User.Name)
            .Select(w => new { w.UserId, w.User.Name, w.User.AvatarFileId })
            .AsNoTracking()
            .ToListAsync();

        var watchers = rows.Select(r => new TaskWatcherDto
        {
            UserId = r.UserId,
            Name = r.Name,
            AvatarUrl = UserMapper.AvatarUrl(r.UserId, r.AvatarFileId),
        }).ToList();

        return new TaskWatchersDto
        {
            Watchers = watchers,
            IsWatching = watchers.Any(w => w.UserId == userId),
        };
    }

    private Task<Guid?> ProjectIdOfTaskAsync(Guid taskId) =>
        _context.ProjectTasks.Where(t => t.Id == taskId).Select(t => (Guid?)t.ProjectId).FirstOrDefaultAsync();
}
