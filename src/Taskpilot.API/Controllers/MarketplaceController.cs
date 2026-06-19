using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Marketplace;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// REST endpoints for the marketplace: tasks and applications. All require authentication.
/// </summary>
[ApiController]
[Authorize]
[Route("api/marketplace")]
public class MarketplaceController : BaseApiController
{
    private readonly IMarketplaceService _marketplace;
    private readonly IValidator<CreateTaskDto> _createTaskValidator;
    private readonly IValidator<ApplyDto> _applyValidator;

    public MarketplaceController(
        IMarketplaceService marketplace,
        IValidator<CreateTaskDto> createTaskValidator,
        IValidator<ApplyDto> applyValidator)
    {
        _marketplace = marketplace;
        _createTaskValidator = createTaskValidator;
        _applyValidator = applyValidator;
    }

    /// <summary>Lists marketplace tasks (open first, then newest).</summary>
    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks()
    {
        var result = await _marketplace.GetTasksAsync();
        return Ok(result.Value);
    }

    /// <summary>Returns a task with its applications.</summary>
    [HttpGet("tasks/{taskId:guid}")]
    public async Task<IActionResult> GetTask(Guid taskId)
    {
        var result = await _marketplace.GetTaskAsync(taskId);
        return result.Succeeded
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>Posts a new task.</summary>
    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _createTaskValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) });

        var result = await _marketplace.CreateTaskAsync(userId.Value, dto);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Applies to a task.</summary>
    [HttpPost("applications")]
    public async Task<IActionResult> Apply([FromBody] ApplyDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _applyValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) });

        var result = await _marketplace.ApplyAsync(userId.Value, dto);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Accepts an application (task poster only).</summary>
    [HttpPost("applications/{applicationId:guid}/accept")]
    public Task<IActionResult> Accept(Guid applicationId) => DecideAsync(applicationId, accept: true);

    /// <summary>Rejects an application (task poster only).</summary>
    [HttpPost("applications/{applicationId:guid}/reject")]
    public Task<IActionResult> Reject(Guid applicationId) => DecideAsync(applicationId, accept: false);

    private async Task<IActionResult> DecideAsync(Guid applicationId, bool accept)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _marketplace.DecideApplicationAsync(userId.Value, applicationId, accept);
        return result.Succeeded
            ? Ok(new { message = accept ? "Application accepted." : "Application rejected." })
            : BadRequest(new { error = result.Error });
    }
}
