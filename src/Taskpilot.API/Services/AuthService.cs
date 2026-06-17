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
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// Creates the service. Dependencies are supplied by the DI container.
    /// </summary>
    /// <param name="context">EF Core database context.</param>
    /// <param name="logger">Structured logger for this service.</param>
    public AuthService(TaskpilotDbContext context, ILogger<AuthService> logger)
    {
        _context = context;
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
}
