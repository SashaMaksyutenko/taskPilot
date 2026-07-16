using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using WebPushSubscription = WebPush.PushSubscription;

namespace Taskpilot.API.Services;

/// <summary>
/// Web Push implementation: stores subscriptions in the database and sends VAPID-signed,
/// encrypted payloads via the <c>WebPush</c> library. Sending is a no-op when VAPID keys
/// are not configured. Subscriptions that the push service reports as gone are removed.
/// </summary>
public class PushService : IPushService
{
    private readonly TaskpilotDbContext _context;
    private readonly VapidOptions _options;
    private readonly ILogger<PushService> _logger;

    public PushService(TaskpilotDbContext context, IOptions<VapidOptions> options, ILogger<PushService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsConfigured;

    /// <inheritdoc />
    public string PublicKey => _options.PublicKey;

    /// <inheritdoc />
    public async Task<Result> SubscribeAsync(Guid userId, string endpoint, string p256dh, string auth)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return Result.Fail("Endpoint is required.");

        // Upsert by endpoint so re-subscribing doesn't create duplicates.
        var existing = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (existing is null)
        {
            _context.PushSubscriptions.Add(new Models.PushSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.UserId = userId;
            existing.P256dh = p256dh;
            existing.Auth = auth;
        }

        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> UnsubscribeAsync(Guid userId, string endpoint)
    {
        await _context.PushSubscriptions
            .Where(s => s.UserId == userId && s.Endpoint == endpoint)
            .ExecuteDeleteAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task SendToUserAsync(Guid userId, string title, string body, string? url)
    {
        if (!IsEnabled)
            return;

        var subscriptions = await _context.PushSubscriptions
            .Where(s => s.UserId == userId)
            .AsNoTracking()
            .ToListAsync();
        if (subscriptions.Count == 0)
            return;

        var payload = JsonSerializer.Serialize(new { title, body, url });
        var vapid = new VapidDetails(_options.Subject, _options.PublicKey, _options.PrivateKey);
        var client = new WebPushClient();

        foreach (var sub in subscriptions)
        {
            try
            {
                var push = new WebPushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(push, payload, vapid);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound
                                              || ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // The browser subscription expired — drop it.
                await _context.PushSubscriptions.Where(s => s.Id == sub.Id).ExecuteDeleteAsync();
                _logger.LogInformation("Removed expired push subscription. SubId: {SubId}", sub.Id);
            }
            catch (Exception ex)
            {
                // Best-effort — never let a push failure break the caller.
                _logger.LogError(ex, "Failed to send push to subscription {SubId}.", sub.Id);
            }
        }
    }
}
