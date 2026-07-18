using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Taskpilot.API.Common;
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
    private static AuthService CreateService(
        TaskpilotDbContext context,
        IGoogleAuthClient? googleClient = null,
        IGitHubAuthClient? gitHubClient = null,
        ILinkedInAuthClient? linkedInClient = null)
    {
        var tokenMock = new Mock<ITokenService>();
        // Access token is a fixed stub; refresh token is unique each call.
        tokenMock.Setup(t => t.GenerateAccessToken(It.IsAny<User>()))
                 .Returns(("access-token", DateTime.UtcNow.AddMinutes(15)));
        tokenMock.Setup(t => t.GenerateRefreshToken())
                 .Returns(() => Guid.NewGuid().ToString("N"));

        var jwt = Options.Create(new JwtSettings { RefreshTokenDays = 7 });
        // Default OAuth clients just fail; tests that exercise a provider pass their own stub.
        var google = googleClient ?? new Mock<IGoogleAuthClient>().Object;
        var gitHub = gitHubClient ?? new Mock<IGitHubAuthClient>().Object;
        var linkedIn = linkedInClient ?? new Mock<ILinkedInAuthClient>().Object;
        var webhooks = new Mock<IWebhookService>().Object;
        return new AuthService(context, tokenMock.Object, google, gitHub, linkedIn, webhooks, jwt, NullLogger<AuthService>.Instance);
    }

    /// <summary>A LinkedIn client stub that always returns the given profile.</summary>
    private static ILinkedInAuthClient LinkedInStub(string sub, string email, string name)
    {
        var mock = new Mock<ILinkedInAuthClient>();
        mock.Setup(c => c.ExchangeCodeAsync(It.IsAny<string>()))
            .ReturnsAsync(Result<LinkedInUserInfo>.Ok(new LinkedInUserInfo(sub, email, name)));
        return mock.Object;
    }

    /// <summary>A Google client stub that always returns the given profile.</summary>
    private static IGoogleAuthClient GoogleStub(string sub, string email, string name)
    {
        var mock = new Mock<IGoogleAuthClient>();
        mock.Setup(c => c.ExchangeCodeAsync(It.IsAny<string>()))
            .ReturnsAsync(Result<GoogleUserInfo>.Ok(new GoogleUserInfo(sub, email, name)));
        return mock.Object;
    }

    /// <summary>A GitHub client stub that always returns the given profile.</summary>
    private static IGitHubAuthClient GitHubStub(string id, string email, string name)
    {
        var mock = new Mock<IGitHubAuthClient>();
        mock.Setup(c => c.ExchangeCodeAsync(It.IsAny<string>()))
            .ReturnsAsync(Result<GitHubUserInfo>.Ok(new GitHubUserInfo(id, email, name)));
        return mock.Object;
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
    public async Task RegisterAsync_BlocksAnEmailOutsideTheDomainAllowlist()
    {
        using var ctx = CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            AllowedEmailDomains = "acme.com",
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.RegisterAsync(new RegisterDto
        {
            Name = "Mallory",
            Email = "mallory@evil.com",
            Password = "Secret123",
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Registration is restricted to specific email domains.", result.Error);
        Assert.Equal(0, await ctx.Users.CountAsync());          // no user created
    }

    [Fact]
    public async Task RegisterAsync_AllowsAnEmailOnTheDomainAllowlist()
    {
        using var ctx = CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            AllowedEmailDomains = "acme.com",
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.RegisterAsync(new RegisterDto
        {
            Name = "Alice",
            Email = "alice@acme.com",
            Password = "Secret123",
        });

        Assert.True(result.Succeeded);
        Assert.Equal(1, await ctx.Users.CountAsync());
    }

    [Fact]
    public async Task RegisterAsync_BlocksADomainOnTheDenylist_WhileOthersStillRegister()
    {
        using var ctx = CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            BlockedEmailDomains = "spam.example",
            // Allowlist stays empty: registration is open to everyone EXCEPT the blocked domain.
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var blocked = await svc.RegisterAsync(new RegisterDto
        {
            Name = "Bot",
            Email = "bot@spam.example",
            Password = "Secret123",
        });
        var allowed = await svc.RegisterAsync(new RegisterDto
        {
            Name = "Alice",
            Email = "alice@anywhere.com",
            Password = "Secret123",
        });

        Assert.False(blocked.Succeeded);
        Assert.Equal("This email domain is not allowed to register.", blocked.Error);
        Assert.True(allowed.Succeeded);          // an open allowlist still lets everyone else in
        Assert.Equal(1, await ctx.Users.CountAsync());
    }

    [Fact]
    public async Task RegisterAsync_DenylistWins_EvenWhenTheDomainIsAlsoAllowed()
    {
        using var ctx = CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId,
            AllowedEmailDomains = "acme.com",
            BlockedEmailDomains = "acme.com",   // contradictory config: deny must take priority
        });
        await ctx.SaveChangesAsync();
        var svc = CreateService(ctx);

        var result = await svc.RegisterAsync(new RegisterDto
        {
            Name = "Alice",
            Email = "alice@acme.com",
            Password = "Secret123",
        });

        Assert.False(result.Succeeded);
        Assert.Equal("This email domain is not allowed to register.", result.Error);
        Assert.Equal(0, await ctx.Users.CountAsync());
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

    [Fact]
    public async Task GoogleLoginAsync_NewAccount_CreatesUserAndIssuesTokens()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx, GoogleStub("google-123", "New.User@Gmail.com", "New User"));

        var result = await svc.GoogleLoginAsync("auth-code");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value!.AccessToken);
        // A password-less account linked to Google was created (email normalized to lowercase).
        var user = await ctx.Users.SingleAsync();
        Assert.Equal("new.user@gmail.com", user.Email);
        Assert.Equal("google-123", user.GoogleId);
        Assert.Null(user.PasswordHash);
    }

    [Fact]
    public async Task GoogleLoginAsync_ExistingEmail_LinksAccountWithoutDuplicate()
    {
        await using var ctx = CreateContext();
        // A local account already exists with this email but no Google link yet.
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Local User",
            Email = "person@example.com",
            PasswordHash = "hash",
            Role = Role.Developer,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, GoogleStub("google-999", "person@example.com", "Person"));
        var result = await svc.GoogleLoginAsync("auth-code");

        Assert.True(result.Succeeded);
        // No duplicate user; the existing account is linked to Google and keeps its password.
        var user = await ctx.Users.SingleAsync();
        Assert.Equal("google-999", user.GoogleId);
        Assert.Equal("hash", user.PasswordHash);
    }

    [Fact]
    public async Task GitHubLoginAsync_NewAccount_CreatesUserAndIssuesTokens()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx, gitHubClient: GitHubStub("gh-123", "Dev@Example.com", "Dev"));

        var result = await svc.GitHubLoginAsync("auth-code");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value!.AccessToken);
        var user = await ctx.Users.SingleAsync();
        Assert.Equal("dev@example.com", user.Email);   // normalized to lowercase
        Assert.Equal("gh-123", user.GitHubId);
        Assert.Null(user.PasswordHash);
    }

    [Fact]
    public async Task GitHubLoginAsync_ExistingEmail_LinksAccountWithoutDuplicate()
    {
        await using var ctx = CreateContext();
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Local User",
            Email = "dev@example.com",
            PasswordHash = "hash",
            Role = Role.Developer,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, gitHubClient: GitHubStub("gh-777", "dev@example.com", "Dev"));
        var result = await svc.GitHubLoginAsync("auth-code");

        Assert.True(result.Succeeded);
        var user = await ctx.Users.SingleAsync();
        Assert.Equal("gh-777", user.GitHubId);
        Assert.Equal("hash", user.PasswordHash);
    }

    [Fact]
    public async Task LinkedInLoginAsync_NewAccount_CreatesUserAndIssuesTokens()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx, linkedInClient: LinkedInStub("li-123", "Dev@Example.com", "Dev"));

        var result = await svc.LinkedInLoginAsync("auth-code");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value!.AccessToken);
        var user = await ctx.Users.SingleAsync();
        Assert.Equal("dev@example.com", user.Email);   // normalized to lowercase
        Assert.Equal("li-123", user.LinkedInId);
        Assert.Null(user.PasswordHash);
    }

    [Fact]
    public async Task LinkedInLoginAsync_ExistingEmail_LinksAccountWithoutDuplicate()
    {
        await using var ctx = CreateContext();
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Local User",
            Email = "dev@example.com",
            PasswordHash = "hash",
            Role = Role.Developer,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, linkedInClient: LinkedInStub("li-777", "dev@example.com", "Dev"));
        var result = await svc.LinkedInLoginAsync("auth-code");

        Assert.True(result.Succeeded);
        var user = await ctx.Users.SingleAsync();
        Assert.Equal("li-777", user.LinkedInId);
        Assert.Equal("hash", user.PasswordHash);
    }
}
