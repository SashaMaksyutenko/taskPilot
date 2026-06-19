using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taskpilot.API.Configuration;
using Taskpilot.API.Models;

namespace Taskpilot.API.Data;

/// <summary>
/// Seeds initial data on application startup. Currently ensures the configured
/// admin user exists so there is always a way into the admin panel.
/// </summary>
public static class DataSeeder
{
    /// <summary>
    /// Ensures the admin user from <see cref="AdminSeedSettings"/> exists with the
    /// Admin role. Creates it if missing, or promotes an existing account.
    /// </summary>
    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var context = sp.GetRequiredService<TaskpilotDbContext>();
        var settings = sp.GetRequiredService<IOptions<AdminSeedSettings>>().Value;
        var logger = sp.GetRequiredService<ILogger<TaskpilotDbContext>>();

        // Nothing to seed if no admin credentials are configured.
        if (string.IsNullOrWhiteSpace(settings.Email) || string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogWarning("Admin seed skipped: Admin__Email / Admin__Password not configured.");
            return;
        }

        var email = settings.Email.Trim().ToLowerInvariant();
        var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (existing is null)
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(settings.Name) ? "Administrator" : settings.Name.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(settings.Password),
                Role = Role.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded admin user: {Email}", email);
        }
        else if (existing.Role != Role.Admin)
        {
            existing.Role = Role.Admin;
            existing.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            logger.LogInformation("Promoted existing user to Admin: {Email}", email);
        }
        else
        {
            logger.LogInformation("Admin user already present: {Email}", email);
        }
    }
}
