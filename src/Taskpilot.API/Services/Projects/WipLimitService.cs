using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class WipLimitService : IWipLimitService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<WipLimitService> _logger;

    public WipLimitService(TaskpilotDbContext context, ILogger<WipLimitService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<List<WipLimitDto>>> GetAsync(Guid userId, Guid projectId)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<List<WipLimitDto>>.Fail("Project not found.");

        return Result<List<WipLimitDto>>.Ok(await LoadAsync(projectId));
    }

    /// <inheritdoc />
    public async Task<Result<List<WipLimitDto>>> SetAsync(Guid userId, Guid projectId, SetWipLimitDto dto)
    {
        if (!Enum.TryParse<ProjectTaskStatus>(dto.Status, ignoreCase: true, out var status))
            return Result<List<WipLimitDto>>.Fail("Invalid status.");
        if (!await ProjectAccess.CanWriteAsync(_context, projectId, userId))
            return Result<List<WipLimitDto>>.Fail("You have read-only access to this project.");

        var existing = await _context.ProjectWipLimits
            .FirstOrDefaultAsync(w => w.ProjectId == projectId && w.Status == status);

        // A null/non-positive limit clears the column's limit; otherwise upsert it.
        if (dto.MaxTasks is not { } max || max <= 0)
        {
            if (existing is not null)
                _context.ProjectWipLimits.Remove(existing);
        }
        else if (existing is null)
        {
            _context.ProjectWipLimits.Add(new ProjectWipLimit
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Status = status,
                MaxTasks = max,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.MaxTasks = max;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("WIP limit set. Project: {Project}, Column: {Status}, Max: {Max}", projectId, status, dto.MaxTasks);
        return Result<List<WipLimitDto>>.Ok(await LoadAsync(projectId));
    }

    private async Task<List<WipLimitDto>> LoadAsync(Guid projectId) =>
        await _context.ProjectWipLimits
            .Where(w => w.ProjectId == projectId)
            .Select(w => new WipLimitDto { Status = w.Status.ToString(), MaxTasks = w.MaxTasks })
            .AsNoTracking()
            .ToListAsync();
}
