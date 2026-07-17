using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.DTOs.Admin;
using Taskpilot.API.Middleware;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for <see cref="FeatureGateMiddleware"/>: it blocks a disabled feature's API,
/// lets an enabled one through, and never touches the settings for ungated paths.
/// </summary>
public class FeatureGateMiddlewareTests
{
    /// <summary>Runs the middleware for a path against the given flags; returns (nextCalled, statusCode).</summary>
    private static async Task<(bool nextCalled, int status)> RunAsync(
        string path, FeatureFlagsDto flags, Mock<IOrganizationSettingsService>? settingsMock = null)
    {
        var settings = settingsMock ?? new Mock<IOrganizationSettingsService>();
        settings.Setup(s => s.GetFeatureFlagsAsync()).ReturnsAsync(flags);

        var nextCalled = false;
        var middleware = new FeatureGateMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<FeatureGateMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(ctx, settings.Object);
        return (nextCalled, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task DisabledMarketplace_Blocks_MarketplaceApi()
    {
        var (nextCalled, status) = await RunAsync(
            "/api/marketplace/tasks", new FeatureFlagsDto { MarketplaceEnabled = false, ForumEnabled = true });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public async Task EnabledMarketplace_Allows_MarketplaceApi()
    {
        var (nextCalled, status) = await RunAsync(
            "/api/marketplace/tasks", new FeatureFlagsDto { MarketplaceEnabled = true, ForumEnabled = true });

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, status);   // untouched default
    }

    [Fact]
    public async Task DisabledForum_Blocks_ForumApi()
    {
        var (nextCalled, status) = await RunAsync(
            "/api/forum/topics", new FeatureFlagsDto { MarketplaceEnabled = true, ForumEnabled = false });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public async Task DisabledMarketplace_DoesNotBlock_TheForumApi()
    {
        // Each flag gates only its own feature.
        var (nextCalled, _) = await RunAsync(
            "/api/forum/topics", new FeatureFlagsDto { MarketplaceEnabled = false, ForumEnabled = true });

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task UngatedPath_IsNotBlocked_AndSettingsAreNotRead()
    {
        var settings = new Mock<IOrganizationSettingsService>();
        var (nextCalled, _) = await RunAsync(
            "/api/projects", new FeatureFlagsDto { MarketplaceEnabled = false, ForumEnabled = false }, settings);

        Assert.True(nextCalled);
        // No database hit for a path that no feature gates.
        settings.Verify(s => s.GetFeatureFlagsAsync(), Times.Never);
    }

    [Fact]
    public async Task LookalikePath_IsNotGated()
    {
        // "/api/marketplaces" is a different segment from "/api/marketplace" and must pass.
        var settings = new Mock<IOrganizationSettingsService>();
        var (nextCalled, _) = await RunAsync(
            "/api/marketplaces", new FeatureFlagsDto { MarketplaceEnabled = false, ForumEnabled = false }, settings);

        Assert.True(nextCalled);
        settings.Verify(s => s.GetFeatureFlagsAsync(), Times.Never);
    }
}
