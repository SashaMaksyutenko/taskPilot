using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Authentication endpoints (registration, and later login/refresh/me).
/// Thin layer: validates input, delegates to <see cref="IAuthService"/>,
/// and maps the result to an HTTP response.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterDto> _registerValidator;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Creates the controller. Dependencies are supplied by the DI container.
    /// </summary>
    public AuthController(
        IAuthService authService,
        IValidator<RegisterDto> registerValidator,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="dto">Registration data (name, email, password).</param>
    /// <returns>
    /// 201 Created with the new user's id on success;
    /// 400 Bad Request when validation fails;
    /// 409 Conflict when the email is already in use.
    /// </returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        // Log entry (email only — never log the password).
        _logger.LogInformation("Register endpoint called. Email: {Email}", dto.Email);

        // Validate the payload before touching business logic.
        var validation = await _registerValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Register validation failed. Email: {Email}", dto.Email);
            // Return field-level errors so the client can show them next to inputs.
            var errors = validation.Errors
                .Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            return BadRequest(new { errors });
        }

        // Delegate to the service (uniqueness check, hashing, persistence).
        var result = await _authService.RegisterAsync(dto);
        if (!result.Succeeded)
        {
            // Expected business failure (e.g. email already in use) -> 409 Conflict.
            _logger.LogWarning("Registration failed. Email: {Email}. Reason: {Error}", dto.Email, result.Error);
            return Conflict(new { error = result.Error });
        }

        _logger.LogInformation("User registered via endpoint. UserId: {UserId}", result.Value);
        // 201 Created with the new user's id.
        return StatusCode(StatusCodes.Status201Created, new { id = result.Value });
    }
}
