using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Auth;
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
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// Creates the service. Dependencies are supplied by the DI container.
    /// </summary>
    /// <param name="context">EF Core database context.</param>
    /// <param name="tokenService">Generates JWT access tokens.</param>
    /// <param name="logger">Structured logger for this service.</param>
    public AuthService(TaskpilotDbContext context, ITokenService tokenService, ILogger<AuthService> logger)
    {
        _context = context;
        _tokenService = tokenService;
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
    public async Task<Result<AuthResponseDto>> LoginAsync(LoginDto dto)
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

            // Credentials are valid — issue a signed JWT access token.
            var (token, expiresAtUtc) = _tokenService.GenerateAccessToken(user);

            _logger.LogInformation("Login successful. UserId: {UserId}", user.Id);
            return Result<AuthResponseDto>.Ok(new AuthResponseDto
            {
                AccessToken = token,
                ExpiresAtUtc = expiresAtUtc,
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login. Email: {Email}", dto.Email);
            return Result<AuthResponseDto>.Fail("An unexpected error occurred during login.");
        }
    }
}
