using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Forgot-password flow. Generates a random single-use token (only its hash is
/// stored), emails a reset link, and applies the new password when the token is
/// presented back. Resetting also revokes existing sessions.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(1);

    private readonly TaskpilotDbContext _context;
    private readonly IEmailSender _email;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        TaskpilotDbContext context,
        IEmailSender email,
        IOptions<EmailOptions> emailOptions,
        ILogger<PasswordResetService> logger)
    {
        _context = context;
        _email = email;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RequestResetAsync(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        if (user is null)
        {
            // Don't reveal whether the email exists.
            _logger.LogInformation("Password reset requested for unknown email.");
            return;
        }

        // Invalidate any earlier pending tokens for this user.
        var pending = await _context.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync();
        foreach (var p in pending)
            p.UsedAt = DateTime.UtcNow;

        var rawToken = RandomToken();
        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.Add(TokenTtl),
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        var link = $"{_emailOptions.FrontendBaseUrl.TrimEnd('/')}/reset-password?token={rawToken}";
        var html =
            $"<p>Hi {System.Net.WebUtility.HtmlEncode(user.Name)},</p>" +
            "<p>We received a request to reset your TaskPilot password. " +
            "Click the link below to choose a new one (it expires in 1 hour):</p>" +
            $"<p><a href=\"{link}\">Reset your password</a></p>" +
            "<p>If you didn't request this, you can safely ignore this email.</p>";
        await _email.SendAsync(user.Email, user.Name, "Reset your TaskPilot password", html);

        _logger.LogInformation("Password reset email dispatched. UserId: {UserId}", user.Id);
    }

    /// <inheritdoc />
    public async Task<Result> ResetAsync(string rawToken, string newPassword)
    {
        var policyError = ValidatePassword(newPassword);
        if (policyError is not null)
            return Result.Fail(policyError);

        if (string.IsNullOrWhiteSpace(rawToken))
            return Result.Fail("Invalid or expired reset link.");

        var hash = Hash(rawToken.Trim());
        var token = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (token is null || token.UsedAt is not null || token.ExpiresAt <= DateTime.UtcNow || token.User is null)
            return Result.Fail("Invalid or expired reset link.");

        token.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        token.User.UpdatedAt = DateTime.UtcNow;
        token.UsedAt = DateTime.UtcNow;

        // Revoke active sessions so a compromised account can't stay signed in.
        var activeSessions = await _context.RefreshTokens
            .Where(rt => rt.UserId == token.UserId && rt.RevokedAtUtc == null)
            .ToListAsync();
        foreach (var rt in activeSessions)
            rt.RevokedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Password reset completed. UserId: {UserId}", token.UserId);
        return Result.Ok();
    }

    // Mirrors the registration password policy: 8–100 chars, lower + upper + digit.
    private static string? ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 100)
            return "Password must be 8–100 characters.";
        if (!Regex.IsMatch(password, "[a-z]") || !Regex.IsMatch(password, "[A-Z]") || !Regex.IsMatch(password, "[0-9]"))
            return "Password must contain a lowercase letter, an uppercase letter and a digit.";
        return null;
    }

    private static string RandomToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
