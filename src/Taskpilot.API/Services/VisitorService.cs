using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Database-backed anonymous-visitor analytics. Keeps one row per (UTC day, hashed IP)
/// with a hit counter, so unique-visitor and total-request counts persist across
/// restarts. Only the SHA-256 hash of the IP is stored (no personal data).
/// </summary>
public class VisitorService : IVisitorService
{
    private readonly TaskpilotDbContext _context;

    public VisitorService(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task RecordAsync(string? ip)
    {
        var hash = Hash(string.IsNullOrEmpty(ip) ? "unknown" : ip);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var row = await _context.VisitorHits.FirstOrDefaultAsync(v => v.Day == today && v.IpHash == hash);
        if (row is null)
            _context.VisitorHits.Add(new VisitorHit { Id = Guid.NewGuid(), Day = today, IpHash = hash, Hits = 1 });
        else
            row.Hits++;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two concurrent first-hits from the same IP can collide on the unique
            // (Day, IpHash) index — harmless for analytics, so ignore.
        }
    }

    /// <inheritdoc />
    public Task<int> UniqueVisitorsTodayAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return _context.VisitorHits.CountAsync(v => v.Day == today);
    }

    /// <inheritdoc />
    public async Task<long> TotalVisitsAsync()
    {
        // Sum of per-day hit counters (0 when there are no rows yet).
        return await _context.VisitorHits.SumAsync(v => (long)v.Hits);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
