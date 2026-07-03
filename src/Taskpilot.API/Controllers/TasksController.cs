using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// REST endpoints for project tasks. Create/list are nested under a project;
/// single-task operations use the task id. All require authentication and are
/// scoped to the owner of the parent project.
/// </summary>
[ApiController]
[Authorize]
public class TasksController : BaseApiController
{
    private readonly ITaskService _tasks;
    private readonly IValidator<CreateTaskDto> _createValidator;
    private readonly IValidator<UpdateTaskDto> _updateValidator;

    public TasksController(
        ITaskService tasks,
        IValidator<CreateTaskDto> createValidator,
        IValidator<UpdateTaskDto> updateValidator)
    {
        _tasks = tasks;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>Lists a project's tasks (use ?status=Backlog|InProgress|Review|Done for a Kanban column).</summary>
    [HttpGet("api/projects/{projectId:guid}/tasks")]
    public async Task<IActionResult> GetByProject(Guid projectId, [FromQuery] string? status = null)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.GetTasksAsync(userId.Value, projectId, status);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Lists the current user's overdue tasks (past deadline, not Done).</summary>
    [HttpGet("api/tasks/overdue")]
    public async Task<IActionResult> GetOverdue()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.GetOverdueTasksAsync(userId.Value);
        return Ok(result.Value);
    }

    /// <summary>Exports a project's tasks as a CSV file.</summary>
    [HttpGet("api/projects/{projectId:guid}/tasks/export")]
    public async Task<IActionResult> Export(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.ExportTasksCsvAsync(userId.Value, projectId);
        if (!result.Succeeded)
            return NotFound(new { error = result.Error });

        var bytes = Encoding.UTF8.GetBytes(result.Value!);
        return File(bytes, "text/csv", $"tasks-{projectId}.csv");
    }

    /// <summary>Exports a project's tasks as an Excel (.xlsx) file.</summary>
    [HttpGet("api/projects/{projectId:guid}/tasks/export/xlsx")]
    public async Task<IActionResult> ExportXlsx(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.ExportTasksXlsxAsync(userId.Value, projectId);
        if (!result.Succeeded)
            return NotFound(new { error = result.Error });

        return File(
            result.Value!,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"tasks-{projectId}.xlsx");
    }

    /// <summary>Exports a project's tasks as a PDF file.</summary>
    [HttpGet("api/projects/{projectId:guid}/tasks/export/pdf")]
    public async Task<IActionResult> ExportPdf(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.ExportTasksPdfAsync(userId.Value, projectId);
        if (!result.Succeeded)
            return NotFound(new { error = result.Error });

        return File(result.Value!, "application/pdf", $"tasks-{projectId}.pdf");
    }

    /// <summary>Creates a task in a project.</summary>
    [HttpPost("api/projects/{projectId:guid}/tasks")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateTaskDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) });

        var result = await _tasks.CreateTaskAsync(userId.Value, projectId, dto);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Returns a single task.</summary>
    [HttpGet("api/tasks/{taskId:guid}")]
    public async Task<IActionResult> Get(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.GetTaskAsync(userId.Value, taskId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Lists a task's subtasks (children).</summary>
    [HttpGet("api/tasks/{taskId:guid}/subtasks")]
    public async Task<IActionResult> GetSubtasks(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.GetSubtasksAsync(userId.Value, taskId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Updates a task's editable fields.</summary>
    [HttpPut("api/tasks/{taskId:guid}")]
    public async Task<IActionResult> Update(Guid taskId, [FromBody] UpdateTaskDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _updateValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) });

        var result = await _tasks.UpdateTaskAsync(userId.Value, taskId, dto);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Moves a task to another Kanban status.</summary>
    [HttpPost("api/tasks/{taskId:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid taskId, [FromBody] ChangeStatusDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.ChangeStatusAsync(userId.Value, taskId, dto.Status);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Creates a copy of a task in the same project.</summary>
    [HttpPost("api/tasks/{taskId:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.DuplicateTaskAsync(userId.Value, taskId);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes a task.</summary>
    [HttpDelete("api/tasks/{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.DeleteTaskAsync(userId.Value, taskId);
        return result.Succeeded
            ? Ok(new { message = "Task deleted." })
            : NotFound(new { error = result.Error });
    }
}
