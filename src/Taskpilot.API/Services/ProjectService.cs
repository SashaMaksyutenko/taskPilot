using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles project business logic. A project is private to its owner: every read
/// and write is scoped to the owner so users only touch their own projects.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly TaskpilotDbContext _context;
    private readonly IWebhookService _webhooks;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(TaskpilotDbContext context, IWebhookService webhooks, ILogger<ProjectService> logger)
    {
        _context = context;
        _webhooks = webhooks;
        _logger = logger;
    }

    // Reusable projection Project -> ProjectDto (runs in SQL).
    private static readonly Expression<Func<Project, ProjectDto>> ToDto = p => new ProjectDto
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Color = p.Color,
        OwnerId = p.OwnerId,
        OwnerName = p.Owner.Name,
        TaskCount = p.Tasks.Count,
        IsArchived = p.ArchivedAt != null,
        CreatedAt = p.CreatedAt,
        ArchivedAt = p.ArchivedAt,
    };

    /// <inheritdoc />
    public async Task<Result<ProjectDto>> CreateProjectAsync(Guid ownerId, SaveProjectDto dto)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Color = dto.Color?.Trim(),
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
        };
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        await _webhooks.DispatchAsync(WebhookEvents.ProjectCreated, new
        {
            projectId = project.Id,
            name = project.Name,
            ownerId,
        });

        _logger.LogInformation("Project created. ProjectId: {ProjectId}, OwnerId: {OwnerId}", project.Id, ownerId);
        return Result<ProjectDto>.Ok(await LoadDtoAsync(project.Id));
    }

    /// <inheritdoc />
    public async Task<Result<List<ProjectDto>>> GetProjectsAsync(Guid ownerId, bool includeArchived)
    {
        var projects = await _context.Projects
            .Where(p => p.OwnerId == ownerId && (includeArchived || p.ArchivedAt == null))
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToDto)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<ProjectDto>>.Ok(projects);
    }

    /// <inheritdoc />
    public async Task<Result<ProjectDto>> GetProjectAsync(Guid projectId, Guid userId)
    {
        var dto = await _context.Projects
            .Where(p => p.Id == projectId && p.OwnerId == userId)
            .Select(ToDto)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return dto is null
            ? Result<ProjectDto>.Fail("Project not found.")
            : Result<ProjectDto>.Ok(dto);
    }

    /// <inheritdoc />
    public async Task<Result<ProjectDto>> UpdateProjectAsync(Guid ownerId, Guid projectId, SaveProjectDto dto)
    {
        var project = await GetOwnedAsync(projectId, ownerId);
        if (project is null)
            return Result<ProjectDto>.Fail("Project not found.");

        project.Name = dto.Name.Trim();
        project.Description = dto.Description?.Trim();
        project.Color = dto.Color?.Trim();
        await _context.SaveChangesAsync();

        return Result<ProjectDto>.Ok(await LoadDtoAsync(project.Id));
    }

    /// <inheritdoc />
    public async Task<Result> SetArchivedAsync(Guid ownerId, Guid projectId, bool archived)
    {
        var project = await GetOwnedAsync(projectId, ownerId);
        if (project is null)
            return Result.Fail("Project not found.");

        project.ArchivedAt = archived ? DateTime.UtcNow : null;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Project {State}. ProjectId: {ProjectId}", archived ? "archived" : "restored", projectId);
        return Result.Ok();
    }

    // --- helpers ---

    /// <summary>Loads a tracked project only if it belongs to the given owner.</summary>
    private Task<Project?> GetOwnedAsync(Guid projectId, Guid ownerId) =>
        _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == ownerId);

    /// <summary>Reloads a project as a DTO (with owner name and task count).</summary>
    private async Task<ProjectDto> LoadDtoAsync(Guid projectId) =>
        await _context.Projects.Where(p => p.Id == projectId).Select(ToDto).AsNoTracking().FirstAsync();
}
