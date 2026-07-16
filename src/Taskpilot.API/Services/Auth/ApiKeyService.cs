using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.ApiKeys;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Personal API keys. Raw keys look like "tp_&lt;40 hex&gt;"; only the SHA-256 hash and a
/// short prefix are stored, so a leaked database never exposes usable keys.
/// </summary>
public class ApiKeyService : IApiKeyService
{
    private const string KeyPrefix = "tp_";
    private const int PrefixLength = 11; // "tp_" + 8 chars, shown for identification

    private readonly TaskpilotDbContext _context;

    public ApiKeyService(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Result<CreatedApiKeyDto>> CreateAsync(Guid userId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<CreatedApiKeyDto>.Fail("A name is required.");

        var raw = KeyPrefix + RandomHex(20); // 40 hex chars of entropy
        var entity = new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            KeyHash = Hash(raw),
            Prefix = raw[..PrefixLength],
            CreatedAt = DateTime.UtcNow,
        };
        _context.ApiKeys.Add(entity);
        await _context.SaveChangesAsync();

        return Result<CreatedApiKeyDto>.Ok(new CreatedApiKeyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Prefix = entity.Prefix,
            CreatedAt = entity.CreatedAt,
            LastUsedAt = null,
            Key = raw, // shown once
        });
    }

    /// <inheritdoc />
    public async Task<Result<List<ApiKeyDto>>> ListAsync(Guid userId)
    {
        var keys = await _context.ApiKeys
            .Where(k => k.UserId == userId && k.RevokedAt == null)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto
            {
                Id = k.Id,
                Name = k.Name,
                Prefix = k.Prefix,
                CreatedAt = k.CreatedAt,
                LastUsedAt = k.LastUsedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        return Result<List<ApiKeyDto>>.Ok(keys);
    }

    /// <inheritdoc />
    public async Task<Result> RevokeAsync(Guid userId, Guid keyId)
    {
        var key = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.UserId == userId);
        if (key is null)
            return Result.Fail("API key not found.");
        if (key.RevokedAt is not null)
            return Result.Ok(); // already revoked — idempotent

        key.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<ApiKeyIdentity?> ResolveAsync(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey) || !rawKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
            return null;

        var hash = Hash(rawKey.Trim());
        var key = await _context.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.RevokedAt == null);
        // The key must exist and its owner must be active.
        if (key is null || key.User is null || !key.User.IsActive)
            return null;

        // Best-effort last-used stamp (throttled to once per minute to avoid a write per request).
        if (key.LastUsedAt is null || key.LastUsedAt < DateTime.UtcNow.AddMinutes(-1))
        {
            key.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return new ApiKeyIdentity(key.UserId, key.User.Email, key.User.Role.ToString());
    }

    private static string RandomHex(int bytes)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
