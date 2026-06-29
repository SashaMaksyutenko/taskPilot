using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Endpoints for managing the current user's account (profile, password).
/// </summary>
[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : BaseApiController
{
    private readonly IUserService _userService;
    private readonly IWarningService _warnings;
    private readonly IAppealService _appeals;
    private readonly IValidator<UpdateProfileDto> _updateProfileValidator;
    private readonly IValidator<ChangePasswordDto> _changePasswordValidator;
    private readonly IValidator<CreateAppealDto> _createAppealValidator;

    public UsersController(
        IUserService userService,
        IWarningService warnings,
        IAppealService appeals,
        IValidator<UpdateProfileDto> updateProfileValidator,
        IValidator<ChangePasswordDto> changePasswordValidator,
        IValidator<CreateAppealDto> createAppealValidator)
    {
        _userService = userService;
        _warnings = warnings;
        _appeals = appeals;
        _updateProfileValidator = updateProfileValidator;
        _changePasswordValidator = changePasswordValidator;
        _createAppealValidator = createAppealValidator;
    }

    /// <summary>Searches active users by name or email (to start a chat, assign a task, etc.).</summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _userService.SearchUsersAsync(userId.Value, q ?? string.Empty);
        return Ok(result.Value);
    }

    /// <summary>Returns the public profile of a user by id.</summary>
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetPublicProfile(Guid userId)
    {
        var result = await _userService.GetPublicProfileAsync(userId);
        return result.Succeeded
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>Serves a user's avatar image. Public so it can be used in &lt;img&gt; tags.</summary>
    [AllowAnonymous]
    [HttpGet("{userId:guid}/avatar")]
    public async Task<IActionResult> GetAvatar(Guid userId)
    {
        var result = await _userService.GetAvatarAsync(userId);
        if (!result.Succeeded)
            return NotFound(new { error = result.Error });

        var download = result.Value!;
        return PhysicalFile(download.PhysicalPath, download.ContentType);
    }

    /// <summary>Uploads/replaces the current user's avatar (multipart/form-data, field "file").</summary>
    [HttpPost("me/avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _userService.SetAvatarAsync(userId.Value, file);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Removes the current user's avatar.</summary>
    [HttpDelete("me/avatar")]
    public async Task<IActionResult> RemoveAvatar()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _userService.RemoveAvatarAsync(userId.Value);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Updates the current user's profile.</summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _updateProfileValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) });

        var result = await _userService.UpdateProfileAsync(userId.Value, dto);
        return result.Succeeded
            ? Ok(result.Value)
            : NotFound(new { error = result.Error });
    }

    /// <summary>Lists the current user's moderation warnings (newest first).</summary>
    [HttpGet("me/warnings")]
    public async Task<IActionResult> GetMyWarnings()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _warnings.GetForUserAsync(userId.Value);
        return Ok(result.Value);
    }

    /// <summary>Lists the current user's appeals (newest first).</summary>
    [HttpGet("me/appeals")]
    public async Task<IActionResult> GetMyAppeals()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _appeals.GetMineAsync(userId.Value);
        return Ok(result.Value);
    }

    /// <summary>Files an appeal against a warning.</summary>
    [HttpPost("me/appeals")]
    public async Task<IActionResult> CreateAppeal([FromBody] CreateAppealDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _createAppealValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { error = validation.Errors[0].ErrorMessage });

        var result = await _appeals.CreateAsync(userId.Value, dto);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Changes the current user's password.</summary>
    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var validation = await _changePasswordValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) });

        var result = await _userService.ChangePasswordAsync(userId.Value, dto);
        return result.Succeeded
            ? Ok(new { message = "Password changed." })
            : BadRequest(new { error = result.Error });
    }
}
