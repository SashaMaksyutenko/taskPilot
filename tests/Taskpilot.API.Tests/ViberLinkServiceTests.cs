using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Taskpilot.API.Configuration;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="ViberLinkService"/> using the in-memory EF provider and
/// an in-memory distributed cache (no Redis or network needed).
/// </summary>
public class ViberLinkServiceTests
{
    private static IDistributedCache Cache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static ViberLinkService CreateService(
        Taskpilot.API.Data.TaskpilotDbContext ctx, IDistributedCache cache, bool configured)
    {
        var opts = Options.Create(new ViberOptions { AuthToken = configured ? "token" : "", BotName = "TaskPilot" });
        return new ViberLinkService(ctx, cache, opts, NullLogger<ViberLinkService>.Instance);
    }

    [Fact]
    public async Task CreateLinkCode_WhenNotConfigured_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var service = CreateService(ctx, Cache(), configured: false);

        var result = await service.CreateLinkCodeAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task LinkByCode_WithValidCode_StoresViberId()
    {
        await using var ctx = TestDb.CreateContext();
        var cache = Cache();
        var userId = await TestDb.AddUserAsync(ctx, "Viber User");
        var service = CreateService(ctx, cache, configured: true);

        var code = (await service.CreateLinkCodeAsync(userId)).Value.Code;
        var linked = await service.LinkByCodeAsync(code, "viber-abc-123");

        Assert.True(linked);
        var user = await ctx.Users.FindAsync(userId);
        Assert.Equal("viber-abc-123", user!.ViberId);

        // Status now reflects the link.
        var status = await service.GetStatusAsync(userId);
        Assert.True(status.Value!.Linked);
    }

    [Fact]
    public async Task LinkByCode_WithUnknownCode_ReturnsFalse()
    {
        await using var ctx = TestDb.CreateContext();
        var service = CreateService(ctx, Cache(), configured: true);

        var linked = await service.LinkByCodeAsync("NOPE1234", "viber-x");

        Assert.False(linked);
    }

    [Fact]
    public async Task LinkByCode_IsOneTimeUse()
    {
        await using var ctx = TestDb.CreateContext();
        var cache = Cache();
        var userId = await TestDb.AddUserAsync(ctx, "Viber User");
        var service = CreateService(ctx, cache, configured: true);

        var code = (await service.CreateLinkCodeAsync(userId)).Value.Code;
        Assert.True(await service.LinkByCodeAsync(code, "viber-1"));
        // Second attempt with the same code fails (code consumed).
        Assert.False(await service.LinkByCodeAsync(code, "viber-2"));
    }
}
