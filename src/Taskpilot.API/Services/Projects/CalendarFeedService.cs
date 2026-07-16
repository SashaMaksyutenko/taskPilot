using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;

namespace Taskpilot.API.Services;

/// <summary>Stores and resolves the per-user iCal feed token in the database.</summary>
public class CalendarFeedService : ICalendarFeedService
{
    private readonly TaskpilotDbContext _context;

    public CalendarFeedService(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<string> GetOrCreateTokenAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
                   ?? throw new InvalidOperationException("User not found.");

        if (string.IsNullOrEmpty(user.CalendarFeedToken))
        {
            user.CalendarFeedToken = NewToken();
            await _context.SaveChangesAsync();
        }
        return user.CalendarFeedToken;
    }

    /// <inheritdoc />
    public async Task<string> RegenerateTokenAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
                   ?? throw new InvalidOperationException("User not found.");

        user.CalendarFeedToken = NewToken();
        await _context.SaveChangesAsync();
        return user.CalendarFeedToken;
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolveUserIdAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var id = await _context.Users
            .Where(u => u.CalendarFeedToken == token)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync();
        return id;
    }

    // A long, URL-safe random token.
    private static string NewToken() =>
        (Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
}
