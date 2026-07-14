using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Webhooks;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Stores webhooks and delivers events to them with a signed POST.
/// </summary>
public class WebhookService : IWebhookService
{
    // Deliveries are retried on a transport error or a non-2xx response.
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    private readonly TaskpilotDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        TaskpilotDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<WebhookDto>> CreateAsync(Guid ownerId, CreateWebhookDto dto)
    {
        var webhook = new Webhook
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Url = dto.Url.Trim(),
            Event = dto.Event.Trim(),
            // Generate a strong secret the receiver uses to verify the signature.
            Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        _context.Webhooks.Add(webhook);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Webhook created. Id: {Id}, Event: {Event}", webhook.Id, webhook.Event);
        return Result<WebhookDto>.Ok(Map(webhook));
    }

    /// <inheritdoc />
    public async Task<Result<List<WebhookDto>>> GetForUserAsync(Guid ownerId)
    {
        var hooks = await _context.Webhooks
            .Where(w => w.OwnerId == ownerId)
            .OrderByDescending(w => w.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<WebhookDto>>.Ok(hooks.Select(Map).ToList());
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid ownerId, Guid webhookId)
    {
        var webhook = await _context.Webhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId && w.OwnerId == ownerId);
        if (webhook is null)
            return Result.Fail("Webhook not found.");

        _context.Webhooks.Remove(webhook);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task DispatchAsync(string eventName, object payload)
    {
        var hooks = await _context.Webhooks
            .Where(w => w.Event == eventName && w.IsActive)
            .AsNoTracking()
            .ToListAsync();

        if (hooks.Count == 0)
            return;

        var json = JsonSerializer.Serialize(payload);
        foreach (var hook in hooks)
            await DeliverAsync(hook, eventName, json);
    }

    /// <inheritdoc />
    public async Task<Result<WebhookDto>> SetActiveAsync(Guid ownerId, Guid webhookId, bool isActive)
    {
        var webhook = await _context.Webhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId && w.OwnerId == ownerId);
        if (webhook is null)
            return Result<WebhookDto>.Fail("Webhook not found.");

        webhook.IsActive = isActive;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Webhook {State}. Id: {Id}", isActive ? "resumed" : "paused", webhookId);
        return Result<WebhookDto>.Ok(Map(webhook));
    }

    /// <inheritdoc />
    public async Task<Result<WebhookDeliveryDto>> TestAsync(Guid ownerId, Guid webhookId)
    {
        var webhook = await _context.Webhooks
            .FirstOrDefaultAsync(w => w.Id == webhookId && w.OwnerId == ownerId);
        if (webhook is null)
            return Result<WebhookDeliveryDto>.Fail("Webhook not found.");

        // A sample payload shaped like a real event, so the receiver can be wired up.
        var json = JsonSerializer.Serialize(new
        {
            test = true,
            webhookId = webhook.Id,
            @event = webhook.Event,
            sentAt = DateTime.UtcNow,
        });

        // A test always goes out, even when the webhook is paused.
        var delivery = await DeliverAsync(webhook, webhook.Event, json);
        return Result<WebhookDeliveryDto>.Ok(MapDelivery(delivery));
    }

    /// <inheritdoc />
    public async Task<Result<List<WebhookDeliveryDto>>> GetDeliveriesAsync(Guid ownerId, Guid webhookId, int limit = 20)
    {
        // Clamp so a caller can't ask for an unbounded page.
        if (limit is < 1 or > 100) limit = 20;

        var owns = await _context.Webhooks.AnyAsync(w => w.Id == webhookId && w.OwnerId == ownerId);
        if (!owns)
            return Result<List<WebhookDeliveryDto>>.Fail("Webhook not found.");

        var deliveries = await _context.WebhookDeliveries
            .Where(d => d.WebhookId == webhookId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<WebhookDeliveryDto>>.Ok(deliveries.Select(MapDelivery).ToList());
    }

    /// <summary>
    /// POSTs the signed payload to one webhook, retrying up to <see cref="MaxAttempts"/>
    /// times on a transport error or a non-2xx response, then records the outcome in the
    /// delivery log. Never throws — a bad receiver must not break the caller.
    /// </summary>
    private async Task<WebhookDelivery> DeliverAsync(Webhook hook, string eventName, string json)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        var delivery = new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            WebhookId = hook.Id,
            Event = eventName,
            CreatedAt = DateTime.UtcNow,
        };

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            delivery.Attempts = attempt;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, hook.Url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-Taskpilot-Event", eventName);
                request.Headers.Add("X-Taskpilot-Signature", Sign(hook.Secret, json));

                using var response = await client.SendAsync(request);
                delivery.StatusCode = (int)response.StatusCode;
                delivery.Error = null;
                delivery.Success = response.IsSuccessStatusCode;

                if (delivery.Success)
                {
                    _logger.LogInformation("Webhook delivered. Id: {Id}, Status: {Status}, Attempt: {Attempt}",
                        hook.Id, delivery.StatusCode, attempt);
                    break;
                }

                _logger.LogWarning("Webhook returned {Status}. Id: {Id}, Attempt: {Attempt}",
                    delivery.StatusCode, hook.Id, attempt);
            }
            catch (Exception ex)
            {
                delivery.Success = false;
                delivery.StatusCode = null;
                delivery.Error = ex.Message;
                _logger.LogWarning(ex, "Webhook delivery failed. Id: {Id}, Url: {Url}, Attempt: {Attempt}",
                    hook.Id, hook.Url, attempt);
            }

            // Short linear back-off between attempts; skip the wait after the last one.
            if (attempt < MaxAttempts)
                await Task.Delay(RetryDelay * attempt);
        }

        _context.WebhookDeliveries.Add(delivery);
        await _context.SaveChangesAsync();
        return delivery;
    }

    /// <summary>Computes the lowercase hex HMAC-SHA256 of the payload using the secret.</summary>
    private static string Sign(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static WebhookDto Map(Webhook w) => new()
    {
        Id = w.Id,
        Url = w.Url,
        Event = w.Event,
        Secret = w.Secret,
        IsActive = w.IsActive,
        CreatedAt = w.CreatedAt,
    };

    private static WebhookDeliveryDto MapDelivery(WebhookDelivery d) => new()
    {
        Id = d.Id,
        Event = d.Event,
        Success = d.Success,
        StatusCode = d.StatusCode,
        Error = d.Error,
        Attempts = d.Attempts,
        CreatedAt = d.CreatedAt,
    };
}
