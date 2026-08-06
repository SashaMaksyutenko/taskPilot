using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Calendar;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for Google Calendar push sync with a FAKE Google client (records upserts, hands back
/// stable event ids) and a mocked <see cref="ITaskService"/> for the deadline tasks — so the
/// connect/refresh/push-and-link logic is verified without a real Google account. The disabled
/// (not-configured) and not-connected paths are covered too.
/// </summary>
public class GoogleCalendarSyncServiceTests
{
    /// <summary>Fake Google client: in-memory events keyed by id, deterministic tokens.</summary>
    private sealed class FakeGoogleCalendarClient : IGoogleCalendarClient
    {
        public bool IsEnabled { get; set; } = true;
        public bool ReturnRefreshToken { get; set; } = true;
        public int UpsertCalls { get; private set; }
        public Dictionary<string, GoogleCalendarEventData> Events { get; } = new();
        private int _seq;

        public Task<Result<GoogleTokenResult>> ExchangeCodeAsync(string code, string redirectUri) =>
            Task.FromResult(IsEnabled
                ? Result<GoogleTokenResult>.Ok(new GoogleTokenResult("access-1", ReturnRefreshToken ? "refresh-1" : null, 3600))
                : Result<GoogleTokenResult>.Fail("not configured"));

        public Task<Result<GoogleTokenResult>> RefreshAccessTokenAsync(string refreshToken) =>
            Task.FromResult(IsEnabled
                ? Result<GoogleTokenResult>.Ok(new GoogleTokenResult("access-refreshed", null, 3600))
                : Result<GoogleTokenResult>.Fail("not configured"));

        public Task<Result<string>> UpsertEventAsync(string accessToken, string? eventId, GoogleCalendarEventData ev)
        {
            UpsertCalls++;
            var id = eventId ?? $"evt-{++_seq}";
            Events[id] = ev;
            return Task.FromResult(Result<string>.Ok(id));
        }

        public Task<Result> DeleteEventAsync(string accessToken, string eventId)
        {
            Events.Remove(eventId);
            return Task.FromResult(Result.Ok());
        }
    }

    private static GoogleCalendarSyncService Make(TaskpilotDbContext ctx, IGoogleCalendarClient client, ITaskService tasks) =>
        new(ctx, client, tasks,
            Options.Create(new GoogleOAuthOptions { ClientId = "id", ClientSecret = "secret", RedirectUri = "uri" }),
            NullLogger<GoogleCalendarSyncService>.Instance);

    private static Mock<ITaskService> TasksReturning(Guid userId, params CalendarTaskDto[] tasks)
    {
        var mock = new Mock<ITaskService>();
        mock.Setup(t => t.GetCalendarTasksAsync(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(Result<List<CalendarTaskDto>>.Ok(tasks.ToList()));
        return mock;
    }

    private static CalendarTaskDto CalTask(string title) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        ProjectId = Guid.NewGuid(),
        ProjectName = "Project",
        Status = "InProgress",
        Priority = "High",
        Deadline = DateTime.UtcNow.AddDays(2),
    };

    [Fact]
    public async Task Status_And_Connect_AreDisabled_WithoutConfig()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeGoogleCalendarClient { IsEnabled = false }, TasksReturning(user).Object);

        var status = await svc.GetStatusAsync(user);
        Assert.False(status.Configured);
        Assert.False(status.Connected);

        var connect = await svc.ConnectAsync(user, "code", "uri");
        Assert.False(connect.Succeeded);
    }

    [Fact]
    public async Task Connect_StoresRefreshToken_AndMarksConnected()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeGoogleCalendarClient(), TasksReturning(user).Object);

        var connect = await svc.ConnectAsync(user, "code", "uri");
        Assert.True(connect.Succeeded);

        var status = await svc.GetStatusAsync(user);
        Assert.True(status.Configured);
        Assert.True(status.Connected);
        Assert.Equal("refresh-1", ctx.GoogleCalendarConnections.Single(c => c.UserId == user).RefreshToken);
    }

    [Fact]
    public async Task Connect_Fails_WhenGoogleReturnsNoRefreshToken()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeGoogleCalendarClient { ReturnRefreshToken = false }, TasksReturning(user).Object);

        var connect = await svc.ConnectAsync(user, "code", "uri");
        Assert.False(connect.Succeeded);
        Assert.False((await svc.GetStatusAsync(user)).Connected);
    }

    [Fact]
    public async Task Sync_Fails_WhenNotConnected()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeGoogleCalendarClient(), TasksReturning(user, CalTask("A")).Object);

        var sync = await svc.SyncAsync(user);
        Assert.False(sync.Succeeded);
    }

    [Fact]
    public async Task Sync_PushesEvents_ThenReSync_UpdatesSameEvents_NoDuplicates()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var client = new FakeGoogleCalendarClient();
        var svc = Make(ctx, client, TasksReturning(user, CalTask("A"), CalTask("B")).Object);
        await svc.ConnectAsync(user, "code", "uri");

        var first = await svc.SyncAsync(user);
        Assert.True(first.Succeeded);
        Assert.Equal(2, first.Value!.Created);
        Assert.Equal(0, first.Value.Updated);
        Assert.Equal(2, first.Value.Total);
        Assert.Equal(2, ctx.GoogleCalendarEventLinks.Count(l => l.UserId == user));
        Assert.Equal(2, client.Events.Count);

        var second = await svc.SyncAsync(user);
        Assert.True(second.Succeeded);
        Assert.Equal(0, second.Value!.Created);   // no new events
        Assert.Equal(2, second.Value.Updated);    // existing events updated
        Assert.Equal(2, ctx.GoogleCalendarEventLinks.Count(l => l.UserId == user)); // no duplicate links
        Assert.Equal(2, client.Events.Count);     // same two events
    }

    [Fact]
    public async Task Disconnect_RemovesConnection_AndLinks()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeGoogleCalendarClient(), TasksReturning(user, CalTask("A")).Object);
        await svc.ConnectAsync(user, "code", "uri");
        await svc.SyncAsync(user);

        await svc.DisconnectAsync(user);

        Assert.False((await svc.GetStatusAsync(user)).Connected);
        Assert.Empty(ctx.GoogleCalendarEventLinks.Where(l => l.UserId == user));
    }
}
