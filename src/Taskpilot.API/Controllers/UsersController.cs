using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IValidator<UpdateProfileDto> _updateProfileValidator;
    private readonly IValidator<ChangePasswordDto> _changePasswordValidator;

    public UsersController(
        IUserService userService,
        IValidator<UpdateProfileDto> updateProfileValidator,
        IValidator<ChangePasswordDto> changePasswordValidator)
    {
        _userService = userService;
        _updateProfileValidator = updateProfileValidator;
        _changePasswordValidator = changePasswordValidator;
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
