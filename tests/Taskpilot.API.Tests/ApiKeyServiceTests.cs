using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="ApiKeyService"/> using the in-memory EF provider.
/// Verifies raw keys are hashed (never stored), and validate/revoke behaviour.
/// </summary>
public class ApiKeyServiceTests
{
    [Fact]
    public async Task Create_ReturnsRawKeyOnce_AndStoresOnlyHash()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx, "Dev");
        var service = new ApiKeyService(ctx);

        var result = await service.CreateAsync(userId, "CI pipeline");

        Assert.True(result.Succeeded);
        Assert.StartsWith("tp_", result.Value!.Key);
        // The raw key is not stored — only its hash + a short prefix.
        var stored = await ctx.ApiKeys.FindAsync(result.Value.Id);
        Assert.NotNull(stored);
        Assert.NotEqual(result.Value.Key, stored!.KeyHash);
        Assert.DoesNotContain(stored.KeyHash, result.Value.Key);
        Assert.Equal(result.Value.Key[..11], stored.Prefix);
    }

    [Fact]
    public async Task Resolve_WithValidKey_ReturnsOwnerIdentity()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx, "Dev");
        var service = new ApiKeyService(ctx);
        var raw = (await service.CreateAsync(userId, "key")).Value!.Key;

        var identity = await service.ResolveAsync(raw);

        Assert.NotNull(identity);
        Assert.Equal(userId, identity!.UserId);
    }

    [Fact]
    public async Task Resolve_WithUnknownKey_ReturnsNull()
    {
        await using var ctx = TestDb.CreateContext();
        var service = new ApiKeyService(ctx);

        Assert.Null(await service.ResolveAsync("tp_deadbeef"));
        Assert.Null(await service.ResolveAsync("not-a-key"));
    }

    [Fact]
    public async Task Revoke_MakesKeyUnusableAndHidesItFromList()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx, "Dev");
        var service = new ApiKeyService(ctx);
        var created = (await service.CreateAsync(userId, "key")).Value!;

        var revoke = await service.RevokeAsync(userId, created.Id);
        Assert.True(revoke.Succeeded);

        // Revoked key no longer resolves and is absent from the active list.
        Assert.Null(await service.ResolveAsync(created.Key));
        var list = await service.ListAsync(userId);
        Assert.Empty(list.Value!);
    }

    [Fact]
    public async Task Revoke_ByNonOwner_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var service = new ApiKeyService(ctx);
        var created = (await service.CreateAsync(owner, "key")).Value!;

        var result = await service.RevokeAsync(other, created.Id);

        Assert.False(result.Succeeded);
        // Still usable by the owner.
        Assert.NotNull(await service.ResolveAsync(created.Key));
    }
}
