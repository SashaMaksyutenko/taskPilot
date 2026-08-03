using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class ActivityService : IActivityService
{
    private readonly TaskpilotDbContext _context;

    public ActivityService(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Result<List<ActivityEntryDto>>> GetProjectActivityAsync(Guid userId, Guid projectId, int limit = 30)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<List<ActivityEntryDto>>.Fail("Project not found.");
        if (limit is < 1 or > 100) limit = 30;

        // Audit entries are keyed by task id (as a string); scope to this project's tasks.
        var taskIds = await _context.ProjectTasks
            .Where(t => t.ProjectId == projectId)
            .Select(t => t.Id.ToString())
            .ToListAsync();
        if (taskIds.Count == 0)
            return Result<List<ActivityEntryDto>>.Ok(new List<ActivityEntryDto>());

        var logs = await _context.AuditLogs
            .Where(a => a.EntityType == nameof(ProjectTask) && a.EntityId != null && taskIds.Contains(a.EntityId))
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new { a.Id, a.Action, a.Details, a.CreatedAt, a.ActorId, a.EntityId })
            .AsNoTracking()
            .ToListAsync();

        // Resolve actor names/avatars in one query.
        var actorIds = logs.Where(l => l.ActorId != null).Select(l => l.ActorId!.Value).Distinct().ToList();
        var actors = await _context.Users
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.AvatarFileId })
            .AsNoTracking()
            .ToListAsync();
        var actorById = actors.ToDictionary(u => u.Id);

        var entries = logs.Select(l =>
        {
            var name = "Deleted user";
            string? avatar = null;
            if (l.ActorId is { } aid && actorById.TryGetValue(aid, out var u))
            {
                name = u.Name;
                avatar = UserMapper.AvatarUrl(aid, u.AvatarFileId);
            }
            return new ActivityEntryDto
            {
                Id = l.Id,
                Action = l.Action,
                Details = l.Details,
                CreatedAt = l.CreatedAt,
                TaskId = Guid.TryParse(l.EntityId, out var g) ? g : null,
                ActorId = l.ActorId,
                ActorName = name,
                ActorAvatarUrl = avatar,
            };
        }).ToList();

        return Result<List<ActivityEntryDto>>.Ok(entries);
    }
}
