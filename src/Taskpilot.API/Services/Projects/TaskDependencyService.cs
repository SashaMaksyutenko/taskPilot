using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class TaskDependencyService : ITaskDependencyService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<TaskDependencyService> _logger;

    public TaskDependencyService(TaskpilotDbContext context, ILogger<TaskDependencyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<TaskDependenciesDto>> GetAsync(Guid userId, Guid taskId)
    {
        var projectId = await ProjectIdOfTaskAsync(taskId);
        if (projectId is null || !await ProjectAccess.CanAccessAsync(_context, projectId.Value, userId))
            return Result<TaskDependenciesDto>.Fail("Task not found.");

        return Result<TaskDependenciesDto>.Ok(await BuildGraphAsync(taskId));
    }

    /// <inheritdoc />
    public async Task<Result<TaskDependenciesDto>> AddAsync(Guid userId, Guid taskId, Guid dependsOnTaskId)
    {
        if (taskId == dependsOnTaskId)
            return Result<TaskDependenciesDto>.Fail("A task cannot depend on itself.");

        var projectId = await ProjectIdOfTaskAsync(taskId);
        if (projectId is null)
            return Result<TaskDependenciesDto>.Fail("Task not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, projectId.Value, userId))
            return Result<TaskDependenciesDto>.Fail("You have read-only access to this project.");

        // The blocker must exist and live in the same project.
        var blockerProject = await ProjectIdOfTaskAsync(dependsOnTaskId);
        if (blockerProject != projectId)
            return Result<TaskDependenciesDto>.Fail("Both tasks must be in the same project.");

        if (await _context.TaskDependencies.AnyAsync(d => d.TaskId == taskId && d.DependsOnTaskId == dependsOnTaskId))
            return Result<TaskDependenciesDto>.Fail("That dependency already exists.");

        if (await WouldCreateCycleAsync(projectId.Value, taskId, dependsOnTaskId))
            return Result<TaskDependenciesDto>.Fail("That would create a circular dependency.");

        _context.TaskDependencies.Add(new TaskDependency
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            DependsOnTaskId = dependsOnTaskId,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task dependency added. Task: {Task}, DependsOn: {DependsOn}", taskId, dependsOnTaskId);
        return Result<TaskDependenciesDto>.Ok(await BuildGraphAsync(taskId));
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(Guid userId, Guid taskId, Guid dependsOnTaskId)
    {
        var projectId = await ProjectIdOfTaskAsync(taskId);
        if (projectId is null)
            return Result.Fail("Task not found.");
        if (!await ProjectAccess.CanWriteAsync(_context, projectId.Value, userId))
            return Result.Fail("You have read-only access to this project.");

        var edge = await _context.TaskDependencies
            .FirstOrDefaultAsync(d => d.TaskId == taskId && d.DependsOnTaskId == dependsOnTaskId);
        if (edge is null)
            return Result.Fail("Dependency not found.");

        _context.TaskDependencies.Remove(edge);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    // --- helpers ---

    private Task<Guid?> ProjectIdOfTaskAsync(Guid taskId) =>
        _context.ProjectTasks.Where(t => t.Id == taskId).Select(t => (Guid?)t.ProjectId).FirstOrDefaultAsync();

    /// <summary>
    /// Adding "task depends on dependsOn" makes a cycle iff dependsOn already (transitively)
    /// depends on task. Walk the depends-on edges from dependsOn and see if we can reach task.
    /// </summary>
    private async Task<bool> WouldCreateCycleAsync(Guid projectId, Guid taskId, Guid dependsOnTaskId)
    {
        var edges = await _context.TaskDependencies
            .Where(d => d.Task.ProjectId == projectId)
            .Select(d => new { d.TaskId, d.DependsOnTaskId })
            .ToListAsync();

        var dependsOnOf = edges
            .GroupBy(e => e.TaskId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.DependsOnTaskId).ToList());

        var stack = new Stack<Guid>();
        stack.Push(dependsOnTaskId);
        var visited = new HashSet<Guid>();
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == taskId)
                return true; // reached the dependent task → cycle
            if (!visited.Add(current))
                continue;
            if (dependsOnOf.TryGetValue(current, out var deps))
                foreach (var d in deps)
                    stack.Push(d);
        }
        return false;
    }

    private async Task<TaskDependenciesDto> BuildGraphAsync(Guid taskId)
    {
        var dependsOn = await _context.TaskDependencies
            .Where(d => d.TaskId == taskId)
            .Select(d => new { d.DependsOnTask.Id, d.DependsOnTask.Title, d.DependsOnTask.Status })
            .ToListAsync();

        var blocks = await _context.TaskDependencies
            .Where(d => d.DependsOnTaskId == taskId)
            .Select(d => new { d.Task.Id, d.Task.Title, d.Task.Status })
            .ToListAsync();

        return new TaskDependenciesDto
        {
            DependsOn = dependsOn.Select(r => new TaskRefDto { Id = r.Id, Title = r.Title, Status = r.Status.ToString() }).ToList(),
            Blocks = blocks.Select(r => new TaskRefDto { Id = r.Id, Title = r.Title, Status = r.Status.ToString() }).ToList(),
            IsBlocked = dependsOn.Any(r => r.Status != ProjectTaskStatus.Done),
        };
    }
}
