using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests the no-signup demo: the config gate, that a created account is seeded + logged in, and
/// that cleanup reclaims expired demo accounts (and their sample projects) while sparing fresh
/// ones and real users.
/// </summary>
public class DemoServiceTests
{
    private static DemoService Make(TaskpilotDbContext ctx, bool enabled, int retentionHours = 24)
    {
        var tokens = new Mock<ITokenService>();
        tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns(("access-token", DateTime.UtcNow.AddMinutes(15)));
        tokens.Setup(t => t.GenerateRefreshToken()).Returns(() => Guid.NewGuid().ToString("N"));
        return new DemoService(ctx, tokens.Object,
            Options.Create(new DemoOptions { Enabled = enabled, RetentionHours = retentionHours }),
            Options.Create(new JwtSettings { RefreshTokenDays = 7 }),
            NullLogger<DemoService>.Instance);
    }

    [Fact]
    public async Task CreateDemo_IsRefused_WhenDisabled()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Make(ctx, enabled: false);

        Assert.False(svc.IsEnabled);
        var result = await svc.CreateDemoAsync("1.2.3.4", "agent");
        Assert.False(result.Succeeded);
        Assert.Empty(ctx.Users);
    }

    [Fact]
    public async Task CreateDemo_SeedsAnAccount_WithSampleData_AndReturnsTokens()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Make(ctx, enabled: true);

        var result = await svc.CreateDemoAsync("1.2.3.4", "agent");

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.Value!.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));

        var user = await ctx.Users.SingleAsync();
        Assert.True(user.IsDemo);
        Assert.True(user.IsActive);
        Assert.Equal(Role.Developer, user.Role);
        Assert.Equal(result.Value.UserId, user.Id);

        // Seeded so the first screen isn't empty: a project, several tasks, and a note.
        Assert.Equal(1, await ctx.Projects.CountAsync(p => p.OwnerId == user.Id));
        Assert.True(await ctx.ProjectTasks.CountAsync() >= 5);
        Assert.True(await ctx.Notes.AnyAsync(n => n.OwnerId == user.Id));
        // A refresh token was issued for the session.
        Assert.True(await ctx.RefreshTokens.AnyAsync(rt => rt.UserId == user.Id));
    }

    [Fact]
    public async Task PurgeExpired_RemovesOldDemoAndItsProjects_ButKeepsFreshDemoAndRealUsers()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Make(ctx, enabled: true, retentionHours: 24);

        // A real user must never be touched.
        var real = await TestDb.AddUserAsync(ctx, "Real");

        // An expired demo (created 48h ago) and a fresh one.
        var oldDemo = (await svc.CreateDemoAsync(null, null)).Value!.UserId;
        var freshDemo = (await svc.CreateDemoAsync(null, null)).Value!.UserId;
        var old = await ctx.Users.FirstAsync(u => u.Id == oldDemo);
        old.CreatedAt = DateTime.UtcNow.AddHours(-48);
        await ctx.SaveChangesAsync();

        var purged = await svc.PurgeExpiredAsync();

        Assert.Equal(1, purged);
        // The expired demo is retired and its sample project is gone.
        var retired = await ctx.Users.FirstAsync(u => u.Id == oldDemo);
        Assert.False(retired.IsDemo);
        Assert.False(retired.IsActive);
        Assert.Empty(ctx.Projects.Where(p => p.OwnerId == oldDemo));
        // The fresh demo and its data survive.
        Assert.True((await ctx.Users.FirstAsync(u => u.Id == freshDemo)).IsDemo);
        Assert.Equal(1, await ctx.Projects.CountAsync(p => p.OwnerId == freshDemo));
        // The real user is untouched.
        Assert.True((await ctx.Users.FirstAsync(u => u.Id == real)).IsActive);
    }
}
