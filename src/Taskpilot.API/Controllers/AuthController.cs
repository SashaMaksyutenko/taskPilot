using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterDto> _registerValidator;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly IPasswordResetService _passwordReset;
    private readonly IAuditService _audit;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Creates the controller. Dependencies are supplied by the DI container.
    /// </summary>
    public AuthController(
        IAuthService authService,
        IValidator<RegisterDto> registerValidator,
        IValidator<LoginDto> loginValidator,
        IPasswordResetService passwordReset,
        IAuditService audit,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _passwordReset = passwordReset;
        _audit = audit;
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
    [EnableRateLimiting("auth")]
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
        // Record the registration in the audit trail.
        await _audit.LogAsync("auth.register", actorId: result.Value, actorEmail: dto.Email,
            entityType: "User", entityId: result.Value.ToString(), ipAddress: ClientIp());
        // 201 Created with the new user's id.
        return StatusCode(StatusCodes.Status201Created, new { id = result.Value });
    }

    /// <summary>
    /// Authenticates a user and returns a JWT access token.
    /// </summary>
    /// <param name="dto">Login data (email, password).</param>
    /// <returns>
    /// 200 OK with the access token and user info on success;
    /// 400 Bad Request when validation fails;
    /// 401 Unauthorized when the credentials are invalid.
    /// </returns>
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        // Log entry (email only — never log the password).
        _logger.LogInformation("Login endpoint called. Email: {Email}", dto.Email);

        // Validate the payload before touching authentication logic.
        var validation = await _loginValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Login validation failed. Email: {Email}", dto.Email);
            var errors = validation.Errors
                .Select(e => new { field = e.PropertyName, message = e.ErrorMessage });
            return BadRequest(new { errors });
        }

        // Delegate to the service (lookup, password check, token generation).
        var result = await _authService.LoginAsync(dto, ClientIp(), UserAgent());
        if (!result.Succeeded)
        {
            // Invalid credentials / disabled account -> 401 Unauthorized.
            _logger.LogWarning("Login failed. Email: {Email}. Reason: {Error}", dto.Email, result.Error);
            // Record the failed attempt (no actor id — the caller is not authenticated).
            await _audit.LogAsync("auth.login.failed", actorEmail: dto.Email,
                details: result.Error, ipAddress: ClientIp());
            return Unauthorized(new { error = result.Error });
        }

        // Password ok but a TOTP code is still required — no tokens issued yet.
        if (result.Value!.RequiresTwoFactor)
            return Ok(result.Value);

        _logger.LogInformation("Login successful via endpoint. UserId: {UserId}", result.Value.UserId);
        // Record the successful login in the audit trail.
        await _audit.LogAsync("auth.login.success", actorId: result.Value.UserId, actorEmail: dto.Email,
            entityType: "User", entityId: result.Value.UserId.ToString(), ipAddress: ClientIp());
        // 200 OK with the access token and user info.
        return Ok(result.Value);
    }

    /// <summary>Signs a user in with a Google OAuth authorization code.</summary>
    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleLoginDto dto)
    {
        _logger.LogInformation("Google login endpoint called.");

        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { error = "Authorization code is required." });

        var result = await _authService.GoogleLoginAsync(dto.Code, ClientIp(), UserAgent());
        if (!result.Succeeded)
        {
            await _audit.LogAsync("auth.login.google.failed", details: result.Error, ipAddress: ClientIp());
            return Unauthorized(new { error = result.Error });
        }

        await _audit.LogAsync("auth.login.google.success", actorId: result.Value!.UserId, actorEmail: result.Value.Email,
            entityType: "User", entityId: result.Value.UserId.ToString(), ipAddress: ClientIp());
        return Ok(result.Value);
    }

    /// <summary>Signs a user in with a GitHub OAuth authorization code.</summary>
    [HttpPost("github")]
    public async Task<IActionResult> GitHub([FromBody] GitHubLoginDto dto)
    {
        _logger.LogInformation("GitHub login endpoint called.");

        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { error = "Authorization code is required." });

        var result = await _authService.GitHubLoginAsync(dto.Code, ClientIp(), UserAgent());
        if (!result.Succeeded)
        {
            await _audit.LogAsync("auth.login.github.failed", details: result.Error, ipAddress: ClientIp());
            return Unauthorized(new { error = result.Error });
        }

        await _audit.LogAsync("auth.login.github.success", actorId: result.Value!.UserId, actorEmail: result.Value.Email,
            entityType: "User", entityId: result.Value.UserId.ToString(), ipAddress: ClientIp());
        return Ok(result.Value);
    }

    /// <summary>Signs a user in with a LinkedIn OAuth authorization code.</summary>
    [HttpPost("linkedin")]
    public async Task<IActionResult> LinkedIn([FromBody] LinkedInLoginDto dto)
    {
        _logger.LogInformation("LinkedIn login endpoint called.");

        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new { error = "Authorization code is required." });

        var result = await _authService.LinkedInLoginAsync(dto.Code, ClientIp(), UserAgent());
        if (!result.Succeeded)
        {
            await _audit.LogAsync("auth.login.linkedin.failed", details: result.Error, ipAddress: ClientIp());
            return Unauthorized(new { error = result.Error });
        }

        await _audit.LogAsync("auth.login.linkedin.success", actorId: result.Value!.UserId, actorEmail: result.Value.Email,
            entityType: "User", entityId: result.Value.UserId.ToString(), ipAddress: ClientIp());
        return Ok(result.Value);
    }

    /// <summary>Sends a password-reset link to the email (always 200, to avoid probing).</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Email))
            await _passwordReset.RequestResetAsync(dto.Email);

        // Generic response regardless of whether the email exists.
        return Ok(new { message = "If that email is registered, a reset link has been sent." });
    }

    /// <summary>Sets a new password using a valid reset token.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var result = await _passwordReset.ResetAsync(dto.Token, dto.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });

        await _audit.LogAsync("auth.password.reset", details: "Password reset via email link", ipAddress: ClientIp());
        return Ok(new { message = "Your password has been reset. You can now sign in." });
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new access token and a rotated refresh token.
    /// </summary>
    /// <param name="dto">Request body containing the refresh token.</param>
    /// <returns>
    /// 200 OK with fresh tokens on success;
    /// 400 Bad Request when the refresh token is missing;
    /// 401 Unauthorized when the refresh token is invalid, expired or revoked.
    /// </returns>
    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        _logger.LogInformation("Refresh endpoint called.");

        // The token is a single required field — a quick guard is enough here.
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            _logger.LogWarning("Refresh rejected: refresh token is missing.");
            return BadRequest(new { error = "Refresh token is required." });
        }

        var result = await _authService.RefreshAsync(dto.RefreshToken, ClientIp(), UserAgent());
        if (!result.Succeeded)
        {
            _logger.LogWarning("Refresh failed. Reason: {Error}", result.Error);
            return Unauthorized(new { error = result.Error });
        }

        _logger.LogInformation("Refresh successful via endpoint. UserId: {UserId}", result.Value!.UserId);
        return Ok(result.Value);
    }

    /// <summary>Lists the current user's active sessions.</summary>
    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _authService.GetSessionsAsync(userId.Value, CurrentRefreshToken());
        return Ok(result.Value);
    }

    /// <summary>Revokes one of the current user's sessions.</summary>
    [Authorize]
    [HttpPost("sessions/{sessionId:guid}/revoke")]
    public async Task<IActionResult> RevokeSession(Guid sessionId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _authService.RevokeSessionAsync(userId.Value, sessionId);
        return result.Succeeded
            ? Ok(new { message = "Session revoked." })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Revokes all of the current user's sessions except this one.</summary>
    [Authorize]
    [HttpPost("sessions/revoke-others")]
    public async Task<IActionResult> RevokeOtherSessions()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _authService.RevokeOtherSessionsAsync(userId.Value, CurrentRefreshToken());
        return result.Succeeded
            ? Ok(new { message = "Other sessions revoked." })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Starts 2FA enrollment; returns the secret + otpauth URI.</summary>
    [Authorize]
    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupTwoFactor()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _authService.SetupTwoFactorAsync(userId.Value);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Enables 2FA after verifying a code.</summary>
    [Authorize]
    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorCodeDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _authService.EnableTwoFactorAsync(userId.Value, dto.Code);
        return result.Succeeded
            ? Ok(new { message = "Two-factor authentication enabled.", backupCodes = result.Value })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Replaces the user's 2FA backup codes with a fresh set.</summary>
    [Authorize]
    [HttpPost("2fa/backup-codes")]
    public async Task<IActionResult> RegenerateBackupCodes()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _authService.RegenerateBackupCodesAsync(userId.Value);
        return result.Succeeded
            ? Ok(new { backupCodes = result.Value })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Returns how many unused backup codes remain.</summary>
    [Authorize]
    [HttpGet("2fa/backup-codes/count")]
    public async Task<IActionResult> BackupCodesCount()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _authService.RemainingBackupCodesAsync(userId.Value);
        return Ok(new { remaining = result.Value });
    }

    /// <summary>Disables 2FA after verifying a code.</summary>
    [Authorize]
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorCodeDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _authService.DisableTwoFactorAsync(userId.Value, dto.Code);
        return result.Succeeded
            ? Ok(new { message = "Two-factor authentication disabled." })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>The client's current refresh token, sent in the X-Refresh-Token header.</summary>
    private string? CurrentRefreshToken()
    {
        var value = Request.Headers["X-Refresh-Token"].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>The caller's User-Agent header (or null).</summary>
    private string? UserAgent()
    {
        var value = Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Returns the profile of the currently authenticated user.
    /// Requires a valid JWT access token in the Authorization header.
    /// </summary>
    /// <returns>
    /// 200 OK with the user profile;
    /// 401 Unauthorized when no/invalid token is provided;
    /// 404 Not Found when the user no longer exists.
    /// </returns>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        // The user id comes from the JWT "sub" claim (see BaseApiController).
        var userId = CurrentUserId();
        if (userId is null)
        {
            _logger.LogWarning("Me endpoint: token has no valid 'sub' claim.");
            return Unauthorized();
        }

        var result = await _authService.GetCurrentUserAsync(userId.Value);
        if (!result.Succeeded)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
