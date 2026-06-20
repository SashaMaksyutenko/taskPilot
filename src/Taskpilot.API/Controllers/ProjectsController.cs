using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// REST endpoints for projects. All require authentication; a user only sees and
/// manages their own projects.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController : BaseApiController
{
    private readonly IProjectService _projects;
    private readonly IValidator<SaveProjectDto> _validator;

    public ProjectsController(IProjectService projects, IValidator<SaveProjectDto> validator)
    {
        _projects = projects;
        _validator = validator;
    }

    /// <summary>Lists the current user's projects (use ?includeArchived=true to include archived).</summary>
    [HttpGet]
    public async Task<IActionResult> GetProjects([FromQuery] bool includeArchived = false)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _projects.GetProjectsAsync(userId.Value, includeArchived);
        return Ok(result.Value);
    }

    /// <summary>Returns a single project.</summary>
    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetProject(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _projects.GetProjectAsync(projectId, userId.Value);
        return result.Succeeded
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>Creates a new project.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveProjectDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (await Invalid(dto) is { } bad) return bad;

        var result = await _projects.CreateProjectAsync(userId.Value, dto);
        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>Updates a project.</summary>
    [HttpPut("{projectId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, [FromBody] SaveProjectDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (await Invalid(dto) is { } bad) return bad;

        var result = await _projects.UpdateProjectAsync(userId.Value, projectId, dto);
        return result.Succeeded
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>Archives a project.</summary>
    [HttpPost("{projectId:guid}/archive")]
    public Task<IActionResult> Archive(Guid projectId) => SetArchived(projectId, archived: true);

    /// <summary>Restores an archived project.</summary>
    [HttpPost("{projectId:guid}/restore")]
    public Task<IActionResult> Restore(Guid projectId) => SetArchived(projectId, archived: false);

    private async Task<IActionResult> SetArchived(Guid projectId, bool archived)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _projects.SetArchivedAsync(userId.Value, projectId, archived);
        return result.Succeeded
            ? Ok(new { message = archived ? "Project archived." : "Project restored." })
            : NotFound(new { error = result.Error });
    }

    /// <summary>Runs validation; returns a 400 result if invalid, otherwise null.</summary>
    private async Task<IActionResult?> Invalid(SaveProjectDto dto)
    {
        var validation = await _validator.ValidateAsync(dto);
        if (validation.IsValid) return null;
        return BadRequest(new { errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) });
    }
}
