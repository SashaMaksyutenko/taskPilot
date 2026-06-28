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

    /// <summary>Lists a page of marketplace tasks (open first, then newest).</summary>
    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _marketplace.GetTasksAsync(page, pageSize);
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

    /// <summary>
    /// Posts a new public task. RBAC: only Managers and Admins may post marketplace
    /// tasks (Developers browse and apply). Viewers are read-only (blocked earlier).
    /// </summary>
    [Authorize(Roles = "Manager,Admin")]
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

    /// <summary>Assignee submits finished work (In Progress → Submitted).</summary>
    [HttpPost("tasks/{taskId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _marketplace.SubmitTaskAsync(userId.Value, taskId);
        return result.Succeeded
            ? Ok(new { message = "Work submitted." })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Poster approves submitted work (Submitted → Completed).</summary>
    [HttpPost("tasks/{taskId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid taskId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _marketplace.ApproveTaskAsync(userId.Value, taskId);
        return result.Succeeded
            ? Ok(new { message = "Task approved." })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Leaves a 1–5 star review for a completed task (poster or assignee).</summary>
    [HttpPost("tasks/{taskId:guid}/rate")]
    public async Task<IActionResult> Rate(Guid taskId, [FromBody] RateDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _marketplace.RateAsync(userId.Value, taskId, dto.Stars, dto.Comment);
        return result.Succeeded
            ? Ok(new { message = "Review saved." })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Returns the reviews left for a task.</summary>
    [HttpGet("tasks/{taskId:guid}/reviews")]
    public async Task<IActionResult> GetReviews(Guid taskId)
    {
        var result = await _marketplace.GetReviewsAsync(taskId);
        return Ok(result.Value);
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
