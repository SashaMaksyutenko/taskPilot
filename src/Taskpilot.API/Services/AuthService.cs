using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles authentication business logic. Currently implements user registration:
/// validates uniqueness of the email, hashes the password with BCrypt and stores the user.
/// </summary>
public class AuthService : IAuthService
{
    private readonly TaskpilotDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// Creates the service. Dependencies are supplied by the DI container.
    /// </summary>
    /// <param name="context">EF Core database context.</param>
    /// <param name="tokenService">Generates JWT access and refresh tokens.</param>
    /// <param name="jwtOptions">JWT settings (used for the refresh-token lifetime).</param>
    /// <param name="logger">Structured logger for this service.</param>
    public AuthService(
        TaskpilotDbContext context,
        ITokenService tokenService,
        IOptions<JwtSettings> jwtOptions,
        ILogger<AuthService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _jwtSettings = jwtOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new user. The password is hashed before storage and is never logged.
    /// </summary>
    /// <param name="dto">Validated registration data.</param>
    /// <returns>Success with the new user's Id, or failure when the email is taken.</returns>
    public async Task<Result<Guid>> RegisterAsync(RegisterDto dto)
    {
        // Log method entry (email only — never log the password).
        _logger.LogInformation("RegisterAsync started. Email: {Email}", dto.Email);

        try
        {
            // Normalize the email for a case-insensitive uniqueness check and storage.
            var email = dto.Email.Trim().ToLowerInvariant();

            // Check whether a user with this email already exists.
            // AnyAsync runs a fast EXISTS query and returns true/false.
            var emailTaken = await _context.Users.AnyAsync(u => u.Email == email);
            if (emailTaken)
            {
                _logger.LogWarning("Registration blocked: email already in use. Email: {Email}", email);
                return Result<Guid>.Fail("Email is already in use.");
            }

            // Hash the password with BCrypt. The raw password is never stored.
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            // Build the new user entity.
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                Email = email,
                PasswordHash = passwordHash,
                Role = Role.Developer,   // every new user starts as a Developer
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Persist the user. SaveChangesAsync writes the INSERT to PostgreSQL.
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User registered successfully. UserId: {UserId}, Email: {Email}", user.Id, email);
            return Result<Guid>.Ok(user.Id);
        }
        catch (Exception ex)
        {
            // Log the full exception, but never include sensitive data (password).
            _logger.LogError(ex, "Unexpected error during registration. Email: {Email}", dto.Email);
            return Result<Guid>.Fail("An unexpected error occurred during registration.");
        }
    }

    /// <summary>
    /// Authenticates a user and issues a JWT access token on success.
    /// Returns a generic error for any credential failure to avoid leaking
    /// whether the email exists.
    /// </summary>
    /// <param name="dto">Validated login data.</param>
    /// <returns>Success with token + user info, or failure with a generic message.</returns>
    public async Task<Result<AuthResponseDto>> LoginAsync(LoginDto dto, string? ip = null, string? userAgent = null)
    {
        // Log method entry (email only — never log the password).
        _logger.LogInformation("LoginAsync started. Email: {Email}", dto.Email);

        try
        {
            // Normalize the email the same way it was stored at registration.
            var email = dto.Email.Trim().ToLowerInvariant();

            // Look up the user. FirstOrDefaultAsync returns the user or null.
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            // Same generic message whether the user is missing or has no password
            // (e.g. OAuth-only account) — do not reveal which case it is.
            if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            {
                _logger.LogWarning("Login failed: no matching credentials. Email: {Email}", email);
                return Result<AuthResponseDto>.Fail("Invalid email or password.");
            }

            // A temporary ban auto-lifts once it expires (checked lazily at login).
            if (!user.IsActive && user.BannedUntil is { } until && until <= DateTime.UtcNow)
            {
                user.IsActive = true;
                user.BannedUntil = null;
                user.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation("Temporary ban expired; reactivating. UserId: {UserId}", user.Id);
            }

            // Disabled/blocked accounts cannot log in.
            if (!user.IsActive)
            {
                _logger.LogWarning("Login blocked: account is inactive. UserId: {UserId}", user.Id);
                return Result<AuthResponseDto>.Fail("Account is disabled.");
            }

            // Compare the supplied password against the stored BCrypt hash.
            var passwordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!passwordValid)
            {
                _logger.LogWarning("Login failed: wrong password. UserId: {UserId}", user.Id);
                return Result<AuthResponseDto>.Fail("Invalid email or password.");
            }

            // Second factor: when 2FA is on, a valid TOTP code is required as well.
            if (user.TwoFactorEnabled)
            {
                if (string.IsNullOrWhiteSpace(dto.TwoFactorCode))
                    return Result<AuthResponseDto>.Ok(new AuthResponseDto { RequiresTwoFactor = true });

                if (!Totp.Verify(user.TwoFactorSecret ?? string.Empty, dto.TwoFactorCode))
                {
                    _logger.LogWarning("Login failed: bad 2FA code. UserId: {UserId}", user.Id);
                    return Result<AuthResponseDto>.Fail("Invalid authentication code.");
                }
            }

            // Credentials are valid — issue a signed JWT access token and a refresh token.
            var (accessToken, accessExpiresAtUtc) = _tokenService.GenerateAccessToken(user);
            var refreshToken = CreateRefreshToken(user.Id, ip, userAgent);
            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Login successful. UserId: {UserId}", user.Id);
            return Result<AuthResponseDto>.Ok(
                BuildAuthResponse(user, accessToken, accessExpiresAtUtc, refreshToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login. Email: {Email}", dto.Email);
            return Result<AuthResponseDto>.Fail("An unexpected error occurred during login.");
        }
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new access token and a rotated refresh token.
    /// The presented token is revoked and replaced, so each refresh token is used only once.
    /// </summary>
    /// <param name="refreshToken">The refresh token previously issued to the client.</param>
    /// <returns>Success with fresh tokens, or failure when the token is invalid/expired/revoked.</returns>
    public async Task<Result<AuthResponseDto>> RefreshAsync(string refreshToken, string? ip = null, string? userAgent = null)
    {
        _logger.LogInformation("RefreshAsync started.");

        try
        {
            // Look up the token together with its owning user.
            var stored = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            // Reject missing, expired or already-revoked tokens with one generic message.
            if (stored is null || !stored.IsActive)
            {
                _logger.LogWarning("Refresh failed: token missing, expired or revoked.");
                return Result<AuthResponseDto>.Fail("Invalid or expired refresh token.");
            }

            var user = stored.User;
            if (!user.IsActive)
            {
                _logger.LogWarning("Refresh blocked: account is inactive. UserId: {UserId}", user.Id);
                return Result<AuthResponseDto>.Fail("Account is disabled.");
            }

            // Rotate: revoke the presented token and issue a brand-new one, keeping the
            // session's origin (fall back to the presented token's if not supplied).
            stored.RevokedAtUtc = DateTime.UtcNow;
            var newRefreshToken = CreateRefreshToken(user.Id, ip ?? stored.IpAddress, userAgent ?? stored.UserAgent);
            _context.RefreshTokens.Add(newRefreshToken);

            // Issue a new access token and persist the rotation in one transaction.
            var (accessToken, accessExpiresAtUtc) = _tokenService.GenerateAccessToken(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Refresh successful. UserId: {UserId}", user.Id);
            return Result<AuthResponseDto>.Ok(
                BuildAuthResponse(user, accessToken, accessExpiresAtUtc, newRefreshToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh.");
            return Result<AuthResponseDto>.Fail("An unexpected error occurred during token refresh.");
        }
    }

    /// <summary>
    /// Returns the public profile of a user by id (read-only).
    /// </summary>
    /// <param name="userId">Id of the user (taken from the authenticated token).</param>
    /// <returns>Success with the profile, or failure when the user is missing.</returns>
    public async Task<Result<UserDto>> GetCurrentUserAsync(Guid userId)
    {
        _logger.LogInformation("GetCurrentUserAsync started. UserId: {UserId}", userId);

        try
        {
            // AsNoTracking: read-only query, no change tracking (faster).
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
            {
                _logger.LogWarning("Current user not found. UserId: {UserId}", userId);
                return Result<UserDto>.Fail("User not found.");
            }

            return Result<UserDto>.Ok(UserMapper.ToDto(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while loading current user. UserId: {UserId}", userId);
            return Result<UserDto>.Fail("An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Creates a new refresh-token entity for a user with a random value and a 7-day expiry.
    /// </summary>
    private RefreshToken CreateRefreshToken(Guid userId, string? ip = null, string? userAgent = null) => new()
    {
        Id = Guid.NewGuid(),
        Token = _tokenService.GenerateRefreshToken(),
        UserId = userId,
        CreatedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays),
        IpAddress = ip,
        // Cap the user-agent to a sane length.
        UserAgent = userAgent is { Length: > 400 } ua ? ua[..400] : userAgent,
    };

    /// <inheritdoc />
    public async Task<Result<List<SessionDto>>> GetSessionsAsync(Guid userId, string? currentToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAtUtc == null && rt.ExpiresAtUtc > now)
            .OrderByDescending(rt => rt.CreatedAtUtc)
            .Select(rt => new SessionDto
            {
                Id = rt.Id,
                CreatedAtUtc = rt.CreatedAtUtc,
                ExpiresAtUtc = rt.ExpiresAtUtc,
                IpAddress = rt.IpAddress,
                UserAgent = rt.UserAgent,
                IsCurrent = currentToken != null && rt.Token == currentToken,
            })
            .AsNoTracking()
            .ToListAsync();

        return Result<List<SessionDto>>.Ok(sessions);
    }

    /// <inheritdoc />
    public async Task<Result> RevokeSessionAsync(Guid userId, Guid sessionId)
    {
        var session = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Id == sessionId && rt.UserId == userId);
        if (session is null)
            return Result.Fail("Session not found.");

        if (session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Session revoked. UserId: {UserId}, SessionId: {SessionId}", userId, sessionId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> RevokeOtherSessionsAsync(Guid userId, string? currentToken)
    {
        // Revoke every active session except the caller's current one.
        await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAtUtc == null
                         && (currentToken == null || rt.Token != currentToken))
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAtUtc, DateTime.UtcNow));

        _logger.LogInformation("Other sessions revoked. UserId: {UserId}", userId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<TwoFactorSetupDto>> SetupTwoFactorAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result<TwoFactorSetupDto>.Fail("User not found.");

        if (user.TwoFactorEnabled)
            return Result<TwoFactorSetupDto>.Fail("Two-factor authentication is already enabled.");

        // Fresh secret each time enrollment starts (until confirmed).
        var secret = Totp.GenerateSecret();
        user.TwoFactorSecret = secret;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result<TwoFactorSetupDto>.Ok(new TwoFactorSetupDto
        {
            Secret = secret,
            OtpauthUri = Totp.BuildUri(secret, user.Email),
        });
    }

    /// <inheritdoc />
    public async Task<Result> EnableTwoFactorAsync(Guid userId, string code)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result.Fail("User not found.");

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecret))
            return Result.Fail("Start setup first.");

        if (!Totp.Verify(user.TwoFactorSecret, code))
            return Result.Fail("Invalid authentication code.");

        user.TwoFactorEnabled = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("2FA enabled. UserId: {UserId}", userId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> DisableTwoFactorAsync(Guid userId, string code)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result.Fail("User not found.");

        if (!user.TwoFactorEnabled)
            return Result.Fail("Two-factor authentication is not enabled.");

        if (!Totp.Verify(user.TwoFactorSecret ?? string.Empty, code))
            return Result.Fail("Invalid authentication code.");

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("2FA disabled. UserId: {UserId}", userId);
        return Result.Ok();
    }

    /// <summary>
    /// Builds the auth response returned to the client from the user and freshly issued tokens.
    /// </summary>
    private static AuthResponseDto BuildAuthResponse(
        User user, string accessToken, DateTime accessExpiresAtUtc, RefreshToken refreshToken) => new()
    {
        AccessToken = accessToken,
        ExpiresAtUtc = accessExpiresAtUtc,
        RefreshToken = refreshToken.Token,
        RefreshTokenExpiresAtUtc = refreshToken.ExpiresAtUtc,
        UserId = user.Id,
        Email = user.Email,
        Role = user.Role.ToString()
    };
}
