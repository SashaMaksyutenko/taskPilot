using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Integrations;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class GitHubConnectionService : IGitHubConnectionService
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);

    private readonly TaskpilotDbContext _context;
    private readonly IGitHubConnectClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GitHubConnectionService> _logger;

    public GitHubConnectionService(
        TaskpilotDbContext context,
        IGitHubConnectClient client,
        IMemoryCache cache,
        ILogger<GitHubConnectionService> logger)
    {
        _context = context;
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GitHubConnectionStatusDto> GetStatusAsync(Guid userId)
    {
        var conn = await _context.UserGitHubConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId);
        return new GitHubConnectionStatusDto
        {
            Configured = _client.IsEnabled,
            Connected = conn is not null,
            Login = conn?.Login,
            ConnectedAt = conn?.ConnectedAt,
        };
    }

    /// <inheritdoc />
    public Result<string> BuildConnectUrl(Guid userId, string redirectUri)
    {
        if (!_client.IsEnabled) return Result<string>.Fail("GitHub integration is not configured.");
        if (string.IsNullOrWhiteSpace(redirectUri)) return Result<string>.Fail("A redirect URI is required.");

        // Random CSRF state, tied to this user, echoed back on the callback.
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _cache.Set(StateKey(state), userId, StateTtl);
        return Result<string>.Ok(_client.BuildAuthorizeUrl(redirectUri, state));
    }

    /// <inheritdoc />
    public async Task<Result> ConnectAsync(Guid userId, string code, string redirectUri, string? state)
    {
        if (!_client.IsEnabled) return Result.Fail("GitHub integration is not configured.");
        if (string.IsNullOrWhiteSpace(code)) return Result.Fail("Missing authorization code.");

        // The state must be one we issued to THIS user (blocks login-CSRF that links someone else's account).
        if (string.IsNullOrWhiteSpace(state)
            || !_cache.TryGetValue(StateKey(state), out Guid stateUser)
            || stateUser != userId)
        {
            return Result.Fail("The GitHub sign-in could not be verified. Please try connecting again.");
        }
        _cache.Remove(StateKey(state));

        var exchange = await _client.ExchangeCodeAsync(code, redirectUri);
        if (!exchange.Succeeded) return Result.Fail(exchange.Error!);
        var info = exchange.Value!;

        var conn = await _context.UserGitHubConnections.FirstOrDefaultAsync(c => c.UserId == userId);
        if (conn is null)
        {
            conn = new UserGitHubConnection { UserId = userId };
            _context.UserGitHubConnections.Add(conn);
        }
        conn.AccessToken = info.AccessToken;
        conn.Login = info.Login;
        conn.Scope = info.Scope;
        conn.ConnectedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> DisconnectAsync(Guid userId)
    {
        var conn = await _context.UserGitHubConnections.FirstOrDefaultAsync(c => c.UserId == userId);
        if (conn is not null)
        {
            _context.UserGitHubConnections.Remove(conn);
            await _context.SaveChangesAsync();
        }
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<List<GitHubRepoDto>>> GetReposAsync(Guid userId)
    {
        var conn = await _context.UserGitHubConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId);
        if (conn is null) return Result<List<GitHubRepoDto>>.Fail("GitHub is not connected.");

        var repos = await _client.GetReposAsync(conn.AccessToken);
        if (!repos.Succeeded) return Result<List<GitHubRepoDto>>.Fail(repos.Error!);

        var dtos = repos.Value!
            .Select(r => new GitHubRepoDto { FullName = r.FullName, Private = r.Private })
            .ToList();
        return Result<List<GitHubRepoDto>>.Ok(dtos);
    }

    private static string StateKey(string state) => $"ghconnect:state:{state}";
}
