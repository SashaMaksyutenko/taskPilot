using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for the per-user GitHub account link with a FAKE GitHub client (deterministic token/login,
/// canned repo list) and a real in-memory cache for CSRF state. Covers the disabled path, the
/// connect happy path, state validation (login-CSRF guard), disconnect, and repo listing.
/// </summary>
public class GitHubConnectionServiceTests
{
    /// <summary>Fake GitHub connect client: records the last issued state, hands back a fixed account.</summary>
    private sealed class FakeGitHubConnectClient : IGitHubConnectClient
    {
        public bool IsEnabled { get; set; } = true;
        public string? LastState { get; private set; }
        public List<GitHubRepo> Repos { get; } = new() { new GitHubRepo("octocat/hello", false) };

        public string BuildAuthorizeUrl(string redirectUri, string state)
        {
            LastState = state;
            return $"https://github.com/login/oauth/authorize?state={state}";
        }

        public Task<Result<GitHubTokenResult>> ExchangeCodeAsync(string code, string redirectUri) =>
            Task.FromResult(IsEnabled
                ? Result<GitHubTokenResult>.Ok(new GitHubTokenResult("tok-1", "octocat", "repo"))
                : Result<GitHubTokenResult>.Fail("not configured"));

        public Task<Result<List<GitHubRepo>>> GetReposAsync(string accessToken) =>
            Task.FromResult(Result<List<GitHubRepo>>.Ok(Repos.ToList()));
    }

    private static GitHubConnectionService Make(TaskpilotDbContext ctx, IGitHubConnectClient client) =>
        new(ctx, client, new MemoryCache(new MemoryCacheOptions()), NullLogger<GitHubConnectionService>.Instance);

    [Fact]
    public async Task Status_And_Connect_AreDisabled_WithoutConfig()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeGitHubConnectClient { IsEnabled = false });

        var status = await svc.GetStatusAsync(user);
        Assert.False(status.Configured);
        Assert.False(status.Connected);

        Assert.False(svc.BuildConnectUrl(user, "uri").Succeeded);
        Assert.False((await svc.ConnectAsync(user, "code", "uri", "state")).Succeeded);
    }

    [Fact]
    public async Task Connect_WithValidState_StoresLogin_AndMarksConnected()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var client = new FakeGitHubConnectClient();
        var svc = Make(ctx, client);

        // Issue a real state, then complete the link with it.
        var url = svc.BuildConnectUrl(user, "https://app/cb");
        Assert.True(url.Succeeded);
        var connect = await svc.ConnectAsync(user, "code", "https://app/cb", client.LastState);
        Assert.True(connect.Succeeded);

        var status = await svc.GetStatusAsync(user);
        Assert.True(status.Connected);
        Assert.Equal("octocat", status.Login);
        Assert.Equal("tok-1", ctx.UserGitHubConnections.Single(c => c.UserId == user).AccessToken);
    }

    [Fact]
    public async Task Connect_Fails_WithUnknownState()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var svc = Make(ctx, new FakeGitHubConnectClient());

        var connect = await svc.ConnectAsync(user, "code", "uri", "not-a-real-state");
        Assert.False(connect.Succeeded);
        Assert.False((await svc.GetStatusAsync(user)).Connected);
    }

    [Fact]
    public async Task Connect_Fails_WhenStateBelongsToAnotherUser()
    {
        await using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var client = new FakeGitHubConnectClient();
        var svc = Make(ctx, client);

        // Alice starts the flow; Bob must not be able to complete it with Alice's state.
        svc.BuildConnectUrl(alice, "uri");
        var stolen = client.LastState;
        var connect = await svc.ConnectAsync(bob, "code", "uri", stolen);
        Assert.False(connect.Succeeded);
        Assert.False((await svc.GetStatusAsync(bob)).Connected);
    }

    [Fact]
    public async Task State_IsSingleUse()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var client = new FakeGitHubConnectClient();
        var svc = Make(ctx, client);

        svc.BuildConnectUrl(user, "uri");
        var state = client.LastState;
        Assert.True((await svc.ConnectAsync(user, "code", "uri", state)).Succeeded);
        // The same state cannot be replayed.
        Assert.False((await svc.ConnectAsync(user, "code", "uri", state)).Succeeded);
    }

    [Fact]
    public async Task Disconnect_RemovesConnection()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var client = new FakeGitHubConnectClient();
        var svc = Make(ctx, client);
        svc.BuildConnectUrl(user, "uri");
        await svc.ConnectAsync(user, "code", "uri", client.LastState);

        await svc.DisconnectAsync(user);

        Assert.False((await svc.GetStatusAsync(user)).Connected);
        Assert.Empty(ctx.UserGitHubConnections.Where(c => c.UserId == user));
    }

    [Fact]
    public async Task GetRepos_ReturnsRepos_WhenConnected_AndFails_WhenNot()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var client = new FakeGitHubConnectClient();
        var svc = Make(ctx, client);

        Assert.False((await svc.GetReposAsync(user)).Succeeded); // not connected yet

        svc.BuildConnectUrl(user, "uri");
        await svc.ConnectAsync(user, "code", "uri", client.LastState);

        var repos = await svc.GetReposAsync(user);
        Assert.True(repos.Succeeded);
        Assert.Single(repos.Value!);
        Assert.Equal("octocat/hello", repos.Value![0].FullName);
    }
}
