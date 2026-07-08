using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles Viber account linking. One-time codes are kept in the distributed cache
/// (Redis or in-memory) for 10 minutes, keyed by code.
/// </summary>
public class ViberLinkService : IViberLinkService
{
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(10);
    private const string CachePrefix = "viber-link:";

    private readonly TaskpilotDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ViberOptions _options;
    private readonly ILogger<ViberLinkService> _logger;

    public ViberLinkService(
        TaskpilotDbContext context,
        IDistributedCache cache,
        IOptions<ViberOptions> options,
        ILogger<ViberLinkService> logger)
    {
        _context = context;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<(string Code, string BotName)>> CreateLinkCodeAsync(Guid userId)
    {
        if (!_options.IsConfigured)
            return Result<(string, string)>.Fail("Viber bot is not configured.");

        // Short, unambiguous one-time code.
        var code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await _cache.SetStringAsync(CachePrefix + code, userId.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CodeTtl });

        return Result<(string, string)>.Ok((code, _options.BotName));
    }

    /// <inheritdoc />
    public async Task<bool> LinkByCodeAsync(string code, string viberId)
    {
        var key = CachePrefix + code.Trim().ToUpperInvariant();
        var userIdText = await _cache.GetStringAsync(key);
        if (!Guid.TryParse(userIdText, out var userId))
            return false;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return false;

        user.ViberId = viberId;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(key); // one-time use

        _logger.LogInformation("Viber linked. UserId: {UserId}", userId);
        return true;
    }

    /// <inheritdoc />
    public async Task<Result> UnlinkAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return Result.Fail("User not found.");

        user.ViberId = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<ViberLinkStatus>> GetStatusAsync(Guid userId)
    {
        var viberId = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.ViberId)
            .FirstOrDefaultAsync();

        return Result<ViberLinkStatus>.Ok(new ViberLinkStatus(!string.IsNullOrEmpty(viberId), _options.BotName));
    }
}
