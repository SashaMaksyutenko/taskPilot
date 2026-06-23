using Taskpilot.API.Data;
using Taskpilot.API.Hubs;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="StatsService"/> over an in-memory database.</summary>
public class StatsServiceTests
{
    // Adds a user with an explicit CreatedAt so "newest" ordering is deterministic.
    private static Guid AddUser(TaskpilotDbContext ctx, string name, DateTime createdAt)
    {
        var id = Guid.NewGuid();
        ctx.Users.Add(new User
        {
            Id = id,
            Name = name,
            Email = $"{id:N}@test.local",
            PasswordHash = "hash",
            Role = Role.Developer,
            IsActive = true,
            CreatedAt = createdAt,
        });
        return id;
    }

    [Fact]
    public async Task GetPublicStats_ReturnsCounts_NewestUser_AndOnlineNames()
    {
        using var ctx = TestDb.CreateContext();
        var alice = AddUser(ctx, "Alice", DateTime.UtcNow.AddMinutes(-10));
        var bob = AddUser(ctx, "Bob", DateTime.UtcNow); // most recently registered
        var topicId = Guid.NewGuid();
        ctx.ForumTopics.Add(new ForumTopic { Id = topicId, Title = "T", Body = "B", AuthorId = alice });
        ctx.ForumReplies.Add(new ForumReply { Id = Guid.NewGuid(), TopicId = topicId, AuthorId = alice, Body = "R" });
        await ctx.SaveChangesAsync();

        // Bob is online (one SignalR connection); Alice is not.
        var presence = new PresenceTracker();
        presence.Connected(bob, "conn-1");

        var svc = new StatsService(ctx, presence, new VisitorTracker());
        var result = await svc.GetPublicStatsAsync();

        Assert.True(result.Succeeded);
        var s = result.Value!;
        Assert.Equal(2, s.TotalUsers);
        Assert.Equal("Bob", s.NewestUserName);
        Assert.Equal(1, s.TotalTopics);
        Assert.Equal(1, s.TotalForumPosts);
        Assert.Equal(1, s.OnlineUsers);
        Assert.Equal(new[] { "Bob" }, s.OnlineUserNames);
    }

    [Fact]
    public async Task GetFullStats_IncludesActiveCount_AndVisitorAnalytics()
    {
        using var ctx = TestDb.CreateContext();
        AddUser(ctx, "Alice", DateTime.UtcNow);
        await ctx.SaveChangesAsync();

        var visitors = new VisitorTracker();
        visitors.Record("1.1.1.1");
        visitors.Record("1.1.1.1"); // same IP
        visitors.Record("2.2.2.2");

        var svc = new StatsService(ctx, new PresenceTracker(), visitors);
        var result = await svc.GetFullStatsAsync();

        Assert.True(result.Succeeded);
        var s = result.Value!;
        Assert.Equal(1, s.TotalUsers);
        Assert.Equal(1, s.ActiveUsers);
        Assert.Equal(0, s.OnlineUsers);          // nobody connected
        Assert.Equal(2, s.AnonymousVisitorsToday); // two distinct IPs
        Assert.Equal(3, s.AnonymousVisitsTotal);   // three requests
    }
}
