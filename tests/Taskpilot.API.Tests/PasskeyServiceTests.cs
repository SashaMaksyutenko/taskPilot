using Fido2NetLib;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for the non-cryptographic passkey paths — list, delete, and the guards that run before
/// any FIDO2 verification (expired ceremony, no registered passkey). The attestation/assertion
/// crypto is Fido2NetLib's job and is verified in-browser, not here.
/// </summary>
public class PasskeyServiceTests
{
    private static PasskeyService Make(TaskpilotDbContext ctx) =>
        new(new Mock<IFido2>().Object, ctx, new Mock<IAuthService>().Object,
            new MemoryCache(new MemoryCacheOptions()), NullLogger<PasskeyService>.Instance);

    private static async Task<Guid> AddPasskeyAsync(TaskpilotDbContext ctx, Guid userId, string name)
    {
        var id = Guid.NewGuid();
        ctx.UserPasskeys.Add(new UserPasskey
        {
            Id = id,
            UserId = userId,
            Name = name,
            CredentialId = Guid.NewGuid().ToByteArray(),
            PublicKey = new byte[] { 1, 2, 3 },
            UserHandle = userId.ToByteArray(),
            CredType = "public-key",
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task List_ReturnsUsersPasskeys()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        await AddPasskeyAsync(ctx, user, "Laptop");
        await AddPasskeyAsync(ctx, user, "Phone");

        var list = (await Make(ctx).ListAsync(user)).Value!;
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task Delete_RemovesOwnPasskey_ButNotAnotherUsers()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var pkA = await AddPasskeyAsync(ctx, a, "A-key");
        var pkB = await AddPasskeyAsync(ctx, b, "B-key");
        var svc = Make(ctx);

        Assert.False((await svc.DeleteAsync(b, pkA)).Succeeded); // B can't delete A's
        Assert.True((await svc.DeleteAsync(a, pkA)).Succeeded);  // A deletes their own
        Assert.False(ctx.UserPasskeys.Any(p => p.Id == pkA));
        Assert.True(ctx.UserPasskeys.Any(p => p.Id == pkB));
    }

    [Fact]
    public async Task LoginOptions_Fails_WhenNoPasskeyRegistered()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var email = ctx.Users.Single(u => u.Id == user).Email;

        Assert.False((await Make(ctx).GetLoginOptionsAsync(email)).Succeeded);
    }

    [Fact]
    public async Task CompleteRegister_Fails_WhenCeremonyExpired()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");

        var res = await Make(ctx).CompleteRegisterAsync(user, new PasskeyRegisterCompleteDto { Name = "x" });
        Assert.False(res.Succeeded); // nothing cached from an options call
    }

    [Fact]
    public async Task CompleteLogin_Fails_WhenCeremonyExpired()
    {
        await using var ctx = TestDb.CreateContext();
        var res = await Make(ctx).CompleteLoginAsync(new PasskeyLoginCompleteDto { CeremonyId = "nope" }, null, null);
        Assert.False(res.Succeeded);
    }
}
