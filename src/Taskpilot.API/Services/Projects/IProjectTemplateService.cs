using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Projects;

namespace Taskpilot.API.Services;

/// <summary>
/// Reusable project blueprints (spec module 17: "Projects — defaults, templates").
/// A user can snapshot a project they can access into a template of their own, list and
/// preview their templates, and stamp out fresh projects from them.
/// </summary>
public interface IProjectTemplateService
{
    /// <summary>Snapshots a project's tasks into a new template owned by the user.</summary>
    Task<Result<ProjectTemplateDto>> SaveAsTemplateAsync(Guid userId, Guid projectId, string? name);

    /// <summary>Lists the user's own templates, newest first.</summary>
    Task<Result<List<ProjectTemplateDto>>> GetTemplatesAsync(Guid userId);

    /// <summary>Loads one of the user's templates with its tasks, for previewing.</summary>
    Task<Result<ProjectTemplateDetailDto>> GetTemplateAsync(Guid userId, Guid templateId);

    /// <summary>Creates a new project for the user from one of their templates.</summary>
    Task<Result<ProjectDto>> CreateProjectFromTemplateAsync(Guid userId, Guid templateId, string? name, string? color);

    /// <summary>Deletes one of the user's templates (and its tasks).</summary>
    Task<Result> DeleteTemplateAsync(Guid userId, Guid templateId);
}
