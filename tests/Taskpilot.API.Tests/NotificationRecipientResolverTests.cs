using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for <see cref="NotificationRecipientResolver"/> — the database read that turns a
/// recipient into the contact snapshot the dispatcher (and the notification service) run on.
/// </summary>
public class NotificationRecipientResolverTests
{
    [Fact]
    public async Task Resolve_ReturnsContactDetails_AndEmailMutedFalse_WhenNotOptedOut()
    {
        await using var ctx = TestDb.CreateContext();
        var id = Guid.NewGuid();
        ctx.Users.Add(new User
        {
            Id = id, Name = "Dana", Email = "dana@example.com",
            TelegramChatId = "tg-1", ViberId = "vb-1", Role = Role.Developer, IsActive = true,
        });
        await ctx.SaveChangesAsync();
        var resolver = new NotificationRecipientResolver(ctx);

        var r = await resolver.ResolveAsync(id, NotificationType.Task);

        Assert.Equal("dana@example.com", r.Email);
        Assert.Equal("Dana", r.Name);
        Assert.Equal("tg-1", r.TelegramChatId);
        Assert.Equal("vb-1", r.ViberId);
        Assert.False(r.EmailMuted);
    }

    [Fact]
    public async Task Resolve_ReportsEmailMuted_OnlyForThatType()
    {
        await using var ctx = TestDb.CreateContext();
        var id = Guid.NewGuid();
        ctx.Users.Add(new User { Id = id, Name = "Dana", Email = "d@example.com", Role = Role.Developer, IsActive = true });
        // Email muted for Task, but not for Forum.
        ctx.NotificationPreferences.Add(new NotificationPreference
        {
            Id = Guid.NewGuid(), UserId = id, Type = NotificationType.Task, Channel = NotificationChannel.Email,
        });
        await ctx.SaveChangesAsync();
        var resolver = new NotificationRecipientResolver(ctx);

        Assert.True((await resolver.ResolveAsync(id, NotificationType.Task)).EmailMuted);
        Assert.False((await resolver.ResolveAsync(id, NotificationType.Forum)).EmailMuted);
    }

    [Fact]
    public async Task Resolve_ForAnUnknownUser_ReturnsAnEmptySnapshot()
    {
        await using var ctx = TestDb.CreateContext();
        var resolver = new NotificationRecipientResolver(ctx);

        var r = await resolver.ResolveAsync(Guid.NewGuid(), NotificationType.Task);

        Assert.NotNull(r);
        Assert.Null(r!.Email);
        Assert.Null(r.TelegramChatId);
        Assert.False(r.EmailMuted);
    }

    [Fact]
    public async Task Resolve_InsideQuietHours_ReturnsNull()
    {
        await using var ctx = TestDb.CreateContext();
        var id = Guid.NewGuid();
        var hourNow = DateTime.UtcNow.Hour;
        ctx.Users.Add(new User
        {
            Id = id, Name = "Sleeper", Email = "s@example.com", Role = Role.Developer, IsActive = true,
            // A one-hour window (UTC) that covers the current hour.
            QuietHoursEnabled = true, QuietHoursStart = hourNow, QuietHoursEnd = (hourNow + 1) % 24, TimeZoneId = null,
        });
        await ctx.SaveChangesAsync();
        var resolver = new NotificationRecipientResolver(ctx);

        // Null tells the caller to hold back every out-of-band channel.
        Assert.Null(await resolver.ResolveAsync(id, NotificationType.Task));
    }

    [Fact]
    public async Task Resolve_WithQuietHoursDisabled_ReturnsTheSnapshot()
    {
        await using var ctx = TestDb.CreateContext();
        var id = Guid.NewGuid();
        var hourNow = DateTime.UtcNow.Hour;
        ctx.Users.Add(new User
        {
            Id = id, Name = "Awake", Email = "a@example.com", Role = Role.Developer, IsActive = true,
            // The window would cover now, but the feature is off.
            QuietHoursEnabled = false, QuietHoursStart = hourNow, QuietHoursEnd = (hourNow + 1) % 24,
        });
        await ctx.SaveChangesAsync();
        var resolver = new NotificationRecipientResolver(ctx);

        var r = await resolver.ResolveAsync(id, NotificationType.Task);
        Assert.NotNull(r);
        Assert.Equal("a@example.com", r!.Email);
    }
}
