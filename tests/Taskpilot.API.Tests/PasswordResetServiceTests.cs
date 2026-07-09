using System.Security.Cryptography;
using System.Text;
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

/// <summary>Unit tests for <see cref="PasswordResetService"/> over the in-memory provider.</summary>
public class PasswordResetServiceTests
{
    private static string Hash(string v) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(v))).ToLowerInvariant();

    private static (PasswordResetService svc, Mock<IEmailSender> email) Create(TaskpilotDbContext ctx)
    {
        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        var opts = Options.Create(new EmailOptions { FrontendBaseUrl = "http://localhost:5173" });
        return (new PasswordResetService(ctx, email.Object, opts, NullLogger<PasswordResetService>.Instance), email);
    }

    private static async Task<Guid> AddUserWithPasswordAsync(TaskpilotDbContext ctx, string email)
    {
        var id = Guid.NewGuid();
        ctx.Users.Add(new User
        {
            Id = id, Name = "User", Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassw0rd"),
            Role = Role.Developer, IsActive = true,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    private static PasswordResetToken AddToken(TaskpilotDbContext ctx, Guid userId, string rawToken, DateTime expiresAt, DateTime? usedAt = null)
    {
        var token = new PasswordResetToken
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = Hash(rawToken),
            ExpiresAt = expiresAt, UsedAt = usedAt, CreatedAt = DateTime.UtcNow,
        };
        ctx.PasswordResetTokens.Add(token);
        ctx.SaveChanges();
        return token;
    }

    [Fact]
    public async Task RequestReset_ForExistingUser_CreatesTokenAndSendsEmail()
    {
        await using var ctx = TestDb.CreateContext();
        await AddUserWithPasswordAsync(ctx, "dana@example.com");
        var (svc, email) = Create(ctx);

        await svc.RequestResetAsync("Dana@Example.com"); // case-insensitive

        Assert.Equal(1, await ctx.PasswordResetTokens.CountAsync());
        email.Verify(e => e.SendAsync("dana@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RequestReset_ForUnknownEmail_DoesNothing()
    {
        await using var ctx = TestDb.CreateContext();
        var (svc, email) = Create(ctx);

        await svc.RequestResetAsync("nobody@example.com");

        Assert.Equal(0, await ctx.PasswordResetTokens.CountAsync());
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Reset_WithValidToken_ChangesPasswordAndConsumesToken()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await AddUserWithPasswordAsync(ctx, "dana@example.com");
        AddToken(ctx, userId, "raw-token-123", DateTime.UtcNow.AddMinutes(30));
        var (svc, _) = Create(ctx);

        var result = await svc.ResetAsync("raw-token-123", "NewPassw0rd");

        Assert.True(result.Succeeded);
        var user = await ctx.Users.FindAsync(userId);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassw0rd", user!.PasswordHash));
        Assert.NotNull((await ctx.PasswordResetTokens.SingleAsync()).UsedAt);
    }

    [Fact]
    public async Task Reset_WithExpiredToken_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await AddUserWithPasswordAsync(ctx, "dana@example.com");
        AddToken(ctx, userId, "raw-token-123", DateTime.UtcNow.AddMinutes(-5));
        var (svc, _) = Create(ctx);

        var result = await svc.ResetAsync("raw-token-123", "NewPassw0rd");

        Assert.False(result.Succeeded);
        var user = await ctx.Users.FindAsync(userId);
        Assert.True(BCrypt.Net.BCrypt.Verify("OldPassw0rd", user!.PasswordHash)); // unchanged
    }

    [Fact]
    public async Task Reset_WithUsedToken_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await AddUserWithPasswordAsync(ctx, "dana@example.com");
        AddToken(ctx, userId, "raw-token-123", DateTime.UtcNow.AddMinutes(30), usedAt: DateTime.UtcNow.AddMinutes(-1));
        var (svc, _) = Create(ctx);

        var result = await svc.ResetAsync("raw-token-123", "NewPassw0rd");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Reset_WithWeakPassword_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await AddUserWithPasswordAsync(ctx, "dana@example.com");
        AddToken(ctx, userId, "raw-token-123", DateTime.UtcNow.AddMinutes(30));
        var (svc, _) = Create(ctx);

        var result = await svc.ResetAsync("raw-token-123", "weak"); // too short, no upper/digit

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Reset_RevokesActiveSessions()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await AddUserWithPasswordAsync(ctx, "dana@example.com");
        AddToken(ctx, userId, "raw-token-123", DateTime.UtcNow.AddMinutes(30));
        ctx.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = userId, Token = "rt-1", ExpiresAtUtc = DateTime.UtcNow.AddDays(7) });
        await ctx.SaveChangesAsync();
        var (svc, _) = Create(ctx);

        await svc.ResetAsync("raw-token-123", "NewPassw0rd");

        Assert.NotNull((await ctx.RefreshTokens.SingleAsync()).RevokedAtUtc);
    }
}
