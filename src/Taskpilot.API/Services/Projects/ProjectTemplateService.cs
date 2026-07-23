using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Reusable project blueprints. Saving a project as a template captures its tasks — titles,
/// descriptions, priorities, tags, subtask structure and RELATIVE deadlines — but not
/// per-instance state (status, assignee, absolute dates). Creating a project from a template
/// stamps those tasks out fresh in the Backlog, owned by the user.
/// </summary>
public class ProjectTemplateService : IProjectTemplateService
{
    private readonly TaskpilotDbContext _context;
    private readonly IProjectService _projects;
    private readonly IWebhookService _webhooks;
    private readonly ILogger<ProjectTemplateService> _logger;

    public ProjectTemplateService(
        TaskpilotDbContext context,
        IProjectService projects,
        IWebhookService webhooks,
        ILogger<ProjectTemplateService> logger)
    {
        _context = context;
        _projects = projects;
        _webhooks = webhooks;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ProjectTemplateDto>> SaveAsTemplateAsync(Guid userId, Guid projectId, string? name)
    {
        _logger.LogInformation("SaveAsTemplate started. ProjectId: {ProjectId}, UserId: {UserId}", projectId, userId);

        // Reading a project's tasks to snapshot them needs access to it — owner or member.
        var project = await _context.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Where(ProjectAccess.AccessibleBy(userId))
            .Select(p => new { p.Name, p.Description, p.Color, p.CreatedAt })
            .FirstOrDefaultAsync();
        if (project is null)
            return Result<ProjectTemplateDto>.Fail("Project not found.");

        var template = new ProjectTemplate
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? project.Name : name.Trim(),
            Description = project.Description,
            Color = project.Color,
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
        };
        _context.ProjectTemplates.Add(template);

        var sourceTasks = await _context.ProjectTasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();

        // Remap ids so subtask relationships survive the snapshot, exactly as project
        // duplication does.
        var idMap = sourceTasks.ToDictionary(t => t.Id, _ => Guid.NewGuid());
        foreach (var t in sourceTasks)
        {
            _context.ProjectTemplateTasks.Add(new ProjectTemplateTask
            {
                Id = idMap[t.Id],
                TemplateId = template.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                // Store the deadline relative to the project's start, never below zero, so
                // the template keeps its schedule ("due N days in") rather than a stale date.
                DeadlineOffsetDays = t.Deadline is { } due
                    ? Math.Max(0, (int)Math.Round((due - project.CreatedAt).TotalDays))
                    : null,
                ParentTemplateTaskId = t.ParentTaskId is { } pid && idMap.TryGetValue(pid, out var newPid) ? newPid : null,
                Tags = new List<string>(t.Tags ?? new List<string>()),
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Project saved as template. ProjectId: {ProjectId}, TemplateId: {TemplateId}, Tasks: {Count}",
            projectId, template.Id, sourceTasks.Count);

        return Result<ProjectTemplateDto>.Ok(new ProjectTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Color = template.Color,
            TaskCount = sourceTasks.Count,
            CreatedAt = template.CreatedAt,
        });
    }

    /// <inheritdoc />
    public async Task<Result<List<ProjectTemplateDto>>> GetTemplatesAsync(Guid userId)
    {
        var templates = await _context.ProjectTemplates.AsNoTracking()
            .Where(tpl => tpl.OwnerId == userId)
            .OrderByDescending(tpl => tpl.CreatedAt)
            .Select(tpl => new ProjectTemplateDto
            {
                Id = tpl.Id,
                Name = tpl.Name,
                Description = tpl.Description,
                Color = tpl.Color,
                TaskCount = tpl.Tasks.Count,
                CreatedAt = tpl.CreatedAt,
            })
            .ToListAsync();

        return Result<List<ProjectTemplateDto>>.Ok(templates);
    }

    /// <inheritdoc />
    public async Task<Result<ProjectTemplateDetailDto>> GetTemplateAsync(Guid userId, Guid templateId)
    {
        var template = await _context.ProjectTemplates.AsNoTracking()
            .Where(tpl => tpl.Id == templateId && tpl.OwnerId == userId)
            .Select(tpl => new ProjectTemplateDetailDto
            {
                Id = tpl.Id,
                Name = tpl.Name,
                Description = tpl.Description,
                Color = tpl.Color,
                TaskCount = tpl.Tasks.Count,
                CreatedAt = tpl.CreatedAt,
                Tasks = tpl.Tasks.Select(t => new TemplateTaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Priority = t.Priority.ToString(),
                    DeadlineOffsetDays = t.DeadlineOffsetDays,
                    ParentTemplateTaskId = t.ParentTemplateTaskId,
                    Tags = t.Tags,
                }).ToList(),
            })
            .FirstOrDefaultAsync();

        return template is null
            ? Result<ProjectTemplateDetailDto>.Fail("Template not found.")
            : Result<ProjectTemplateDetailDto>.Ok(template);
    }

    /// <inheritdoc />
    public async Task<Result<ProjectDto>> CreateProjectFromTemplateAsync(Guid userId, Guid templateId, string? name, string? color)
    {
        _logger.LogInformation("CreateProjectFromTemplate started. TemplateId: {TemplateId}, UserId: {UserId}", templateId, userId);

        var template = await _context.ProjectTemplates.AsNoTracking()
            .FirstOrDefaultAsync(tpl => tpl.Id == templateId && tpl.OwnerId == userId);
        if (template is null)
            return Result<ProjectDto>.Fail("Template not found.");

        var now = DateTime.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? template.Name : name.Trim(),
            Description = template.Description,
            Color = string.IsNullOrWhiteSpace(color) ? template.Color : color.Trim(),
            OwnerId = userId,
            CreatedAt = now,
        };
        _context.Projects.Add(project);

        var templateTasks = await _context.ProjectTemplateTasks.AsNoTracking()
            .Where(t => t.TemplateId == templateId)
            .ToListAsync();

        // Remap template-task ids to fresh task ids so subtasks stay linked.
        var idMap = templateTasks.ToDictionary(t => t.Id, _ => Guid.NewGuid());
        foreach (var t in templateTasks)
        {
            _context.ProjectTasks.Add(new ProjectTask
            {
                Id = idMap[t.Id],
                ProjectId = project.Id,
                Title = t.Title,
                Description = t.Description,
                Status = ProjectTaskStatus.Backlog,   // a new project starts clean
                Priority = t.Priority,
                CreatorId = userId,
                ParentTaskId = t.ParentTemplateTaskId is { } pid && idMap.TryGetValue(pid, out var newPid) ? newPid : null,
                // Turn the relative offset back into an absolute deadline from today.
                Deadline = t.DeadlineOffsetDays is { } offset ? now.AddDays(offset) : null,
                Tags = new List<string>(t.Tags ?? new List<string>()),
                CreatedAt = now,
            });
        }

        await _context.SaveChangesAsync();

        await _webhooks.DispatchAsync(WebhookEvents.ProjectCreated, new
        {
            projectId = project.Id,
            name = project.Name,
            ownerId = userId,
        });

        _logger.LogInformation("Project created from template. TemplateId: {TemplateId}, ProjectId: {ProjectId}, Tasks: {Count}",
            templateId, project.Id, templateTasks.Count);

        // Reuse the project service's projection so the DTO matches every other project response.
        return await _projects.GetProjectAsync(project.Id, userId);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteTemplateAsync(Guid userId, Guid templateId)
    {
        var template = await _context.ProjectTemplates
            .FirstOrDefaultAsync(tpl => tpl.Id == templateId && tpl.OwnerId == userId);
        if (template is null)
            return Result.Fail("Template not found.");

        // The template's tasks go with it via the configured cascade delete.
        _context.ProjectTemplates.Remove(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Template deleted. TemplateId: {TemplateId}, By: {UserId}", templateId, userId);
        return Result.Ok();
    }
}
