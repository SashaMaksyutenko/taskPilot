using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Custom fields: project-level definitions and per-task values.</summary>
[ApiController]
[Authorize]
public class CustomFieldsController : BaseApiController
{
    private readonly ICustomFieldService _fields;

    public CustomFieldsController(ICustomFieldService fields)
    {
        _fields = fields;
    }

    /// <summary>Lists a project's custom-field definitions.</summary>
    [HttpGet("api/projects/{projectId:guid}/custom-fields")]
    public async Task<IActionResult> GetDefinitions(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _fields.GetDefinitionsAsync(userId.Value, projectId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Adds a custom-field definition to a project.</summary>
    [HttpPost("api/projects/{projectId:guid}/custom-fields")]
    public async Task<IActionResult> CreateDefinition(Guid projectId, [FromBody] CreateCustomFieldDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _fields.CreateDefinitionAsync(userId.Value, projectId, dto);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes a custom-field definition and every task value for it.</summary>
    [HttpDelete("api/custom-fields/{fieldId:guid}")]
    public async Task<IActionResult> DeleteDefinition(Guid fieldId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _fields.DeleteDefinitionAsync(userId.Value, fieldId);
        return result.Succeeded ? NoContent() : BadRequest(new { error = result.Error });
    }

    /// <summary>A task's custom fields with their current values.</summary>
    [HttpGet("api/tasks/{taskId:guid}/fields")]
    public async Task<IActionResult> GetTaskFields(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _fields.GetTaskFieldsAsync(userId.Value, taskId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Sets or clears a task's value for a field.</summary>
    [HttpPut("api/tasks/{taskId:guid}/fields/{fieldId:guid}")]
    public async Task<IActionResult> SetTaskValue(Guid taskId, Guid fieldId, [FromBody] SetFieldValueDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _fields.SetTaskValueAsync(userId.Value, taskId, fieldId, dto.Value);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
