using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Taskpilot.API.Data;
using Taskpilot.API.Hubs;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="StatsService"/> over an in-memory database.</summary>
public class StatsServiceTests
{
    /// <summary>A real in-memory IDistributedCache for tests (no Redis needed).</summary>
    private static IDistributedCache NewCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

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

        var svc = new StatsService(ctx, presence, new VisitorService(ctx), NewCache());
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

        var visitors = new VisitorService(ctx);
        await visitors.RecordAsync("1.1.1.1");
        await visitors.RecordAsync("1.1.1.1"); // same IP
        await visitors.RecordAsync("2.2.2.2");

        var svc = new StatsService(ctx, new PresenceTracker(), visitors, NewCache());
        var result = await svc.GetFullStatsAsync();

        Assert.True(result.Succeeded);
        var s = result.Value!;
        Assert.Equal(1, s.TotalUsers);
        Assert.Equal(1, s.ActiveUsers);
        Assert.Equal(0, s.OnlineUsers);          // nobody connected
        Assert.Equal(2, s.AnonymousVisitorsToday); // two distinct IPs
        Assert.Equal(3, s.AnonymousVisitsTotal);   // three requests
    }

    [Fact]
    public async Task GetFullStats_IncludesContentMetrics()
    {
        using var ctx = TestDb.CreateContext();
        var owner = AddUser(ctx, "Owner", DateTime.UtcNow);
        var projectId = Guid.NewGuid();
        ctx.Projects.Add(new Project { Id = projectId, Name = "P", OwnerId = owner });
        // 2 Backlog, 1 Done.
        foreach (var st in new[] { ProjectTaskStatus.Backlog, ProjectTaskStatus.Backlog, ProjectTaskStatus.Done })
            ctx.ProjectTasks.Add(new ProjectTask
            {
                Id = Guid.NewGuid(), ProjectId = projectId, CreatorId = owner, Title = "t", Status = st,
            });
        ctx.FileAttachments.Add(new FileAttachment
        {
            Id = Guid.NewGuid(), FileName = "f", StoredName = "s", ContentType = "text/plain",
            SizeBytes = 1000, UploaderId = owner,
        });
        await ctx.SaveChangesAsync();

        var svc = new StatsService(ctx, new PresenceTracker(), new VisitorService(ctx), NewCache());
        var s = (await svc.GetFullStatsAsync()).Value!;

        Assert.Equal(1, s.TotalProjects);
        Assert.Equal(3, s.TotalTasks);
        Assert.Equal(2, s.TasksByStatus["Backlog"]);
        Assert.Equal(1, s.TasksByStatus["Done"]);
        Assert.Equal(0, s.TasksByStatus["Review"]); // every column present, even at zero
        Assert.Equal(1, s.TotalFiles);
        Assert.Equal(1000, s.StorageUsedBytes);
    }
}
