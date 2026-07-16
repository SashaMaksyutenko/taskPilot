using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;

namespace Taskpilot.API.Services;

/// <summary>
/// Shared check used by write paths (chat, forum, comments) to block muted users.
/// A mute is active while <c>MutedUntil</c> is in the future; it expires on its own.
/// </summary>
public static class MuteGuard
{
    /// <summary>
    /// Returns an error message if the user is currently muted, or null if they may post.
    /// </summary>
    public static async Task<string?> CheckAsync(TaskpilotDbContext context, Guid userId)
    {
        var mutedUntil = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.MutedUntil)
            .FirstOrDefaultAsync();

        return mutedUntil is { } until && until > DateTime.UtcNow
            ? $"You are muted until {until:u}."
            : null;
    }
}
