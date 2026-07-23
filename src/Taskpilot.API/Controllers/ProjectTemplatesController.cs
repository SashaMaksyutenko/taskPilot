using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// REST endpoints for reusable project templates. All require authentication; a user only
/// sees and manages their own templates.
/// </summary>
[ApiController]
[Authorize]
public class ProjectTemplatesController : BaseApiController
{
    private readonly IProjectTemplateService _templates;

    public ProjectTemplatesController(IProjectTemplateService templates)
    {
        _templates = templates;
    }

    /// <summary>Lists the current user's templates.</summary>
    [HttpGet("api/project-templates")]
    public async Task<IActionResult> GetTemplates()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _templates.GetTemplatesAsync(userId.Value);
        return Ok(result.Value);
    }

    /// <summary>Returns one template with its tasks, for previewing.</summary>
    [HttpGet("api/project-templates/{templateId:guid}")]
    public async Task<IActionResult> GetTemplate(Guid templateId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _templates.GetTemplateAsync(userId.Value, templateId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Saves an existing project as a new template.</summary>
    [HttpPost("api/projects/{projectId:guid}/save-as-template")]
    public async Task<IActionResult> SaveAsTemplate(Guid projectId, [FromBody] SaveAsTemplateDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _templates.SaveAsTemplateAsync(userId.Value, projectId, dto.Name);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>Creates a new project from a template.</summary>
    [HttpPost("api/project-templates/{templateId:guid}/create-project")]
    public async Task<IActionResult> CreateProject(Guid templateId, [FromBody] CreateFromTemplateDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _templates.CreateProjectFromTemplateAsync(userId.Value, templateId, dto.Name, dto.Color);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>Deletes one of the current user's templates.</summary>
    [HttpDelete("api/project-templates/{templateId:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid templateId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _templates.DeleteTemplateAsync(userId.Value, templateId);
        return result.Succeeded ? NoContent() : NotFound(new { error = result.Error });
    }
}
