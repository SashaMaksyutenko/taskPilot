using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="AuthService"/>. They use the EF Core in-memory provider
/// (a throwaway database per test) and a mocked <see cref="ITokenService"/>, so no real
/// PostgreSQL or JWT signing is involved.
/// </summary>
public class AuthServiceTests
{
    /// <summary>Creates an isolated in-memory database context (unique name per test).</summary>
    private static TaskpilotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TaskpilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TaskpilotDbContext(options);
    }

    /// <summary>Builds an AuthService with a stubbed token service over the given context.</summary>
    private static AuthService CreateService(TaskpilotDbContext context)
    {
        var tokenMock = new Mock<ITokenService>();
        // Access token is a fixed stub; refresh token is unique each call.
        tokenMock.Setup(t => t.GenerateAccessToken(It.IsAny<User>()))
                 .Returns(("access-token", DateTime.UtcNow.AddMinutes(15)));
        tokenMock.Setup(t => t.GenerateRefreshToken())
                 .Returns(() => Guid.NewGuid().ToString("N"));

        var jwt = Options.Create(new JwtSettings { RefreshTokenDays = 7 });
        return new AuthService(context, tokenMock.Object, jwt, NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesUserAndHashesPassword()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);

        var result = await svc.RegisterAsync(new RegisterDto
        {
            Name = "Alice",
            Email = "Alice@Example.com",
            Password = "Secret123"
        });

        Assert.True(result.Succeeded);
        var user = await ctx.Users.SingleAsync();
        Assert.Equal("alice@example.com", user.Email);          // email normalized to lower-case
        Assert.Equal("Alice", user.Name);
        Assert.Equal(Role.Developer, user.Role);                // new users default to Developer
        Assert.NotEqual("Secret123", user.PasswordHash);        // stored value is not the raw password
        Assert.True(BCrypt.Net.BCrypt.Verify("Secret123", user.PasswordHash)); // it is a valid BCrypt hash
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_Fails()
    {
        using var ctx = CreateContext();
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Email = "taken@example.com",
            PasswordHash = "hash"
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.RegisterAsync(new RegisterDto
        {
            Name = "Bob",
            Email = "taken@example.com",
            Password = "Secret123"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Email is already in use.", result.Error);
        Assert.Equal(1, await ctx.Users.CountAsync());          // no second user created
    }

    [Fact]
    public async Task LoginAsync_CorrectCredentials_ReturnsTokensAndStoresRefresh()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.RegisterAsync(new RegisterDto { Name = "Carl", Email = "carl@example.com", Password = "Secret123" });

        var result = await svc.LoginAsync(new LoginDto { Email = "carl@example.com", Password = "Secret123" });

        Assert.True(result.Succeeded);
        Assert.Equal("access-token", result.Value!.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
        Assert.Equal("carl@example.com", result.Value.Email);
        Assert.Equal(1, await ctx.RefreshTokens.CountAsync());  // refresh token persisted
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Fails()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.RegisterAsync(new RegisterDto { Name = "Dora", Email = "dora@example.com", Password = "Secret123" });

        var result = await svc.LoginAsync(new LoginDto { Email = "dora@example.com", Password = "WrongPass1" });

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid email or password.", result.Error);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_Fails()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);

        var result = await svc.LoginAsync(new LoginDto { Email = "nobody@example.com", Password = "Secret123" });

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid email or password.", result.Error);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RotatesAndRevokesOld()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.RegisterAsync(new RegisterDto { Name = "Eve", Email = "eve@example.com", Password = "Secret123" });
        var login = await svc.LoginAsync(new LoginDto { Email = "eve@example.com", Password = "Secret123" });
        var oldToken = login.Value!.RefreshToken;

        var refreshed = await svc.RefreshAsync(oldToken);

        Assert.True(refreshed.Succeeded);
        Assert.NotEqual(oldToken, refreshed.Value!.RefreshToken);            // a new token was issued
        var old = await ctx.RefreshTokens.SingleAsync(t => t.Token == oldToken);
        Assert.NotNull(old.RevokedAtUtc);                                   // old token revoked

        var reuse = await svc.RefreshAsync(oldToken);                       // reusing it now fails
        Assert.False(reuse.Succeeded);
    }
}
