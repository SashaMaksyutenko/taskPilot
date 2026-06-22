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
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        foreach (var hook in hooks)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, hook.Url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-Taskpilot-Event", eventName);
                request.Headers.Add("X-Taskpilot-Signature", Sign(hook.Secret, json));

                using var response = await client.SendAsync(request);
                _logger.LogInformation("Webhook delivered. Id: {Id}, Status: {Status}", hook.Id, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                // One failing receiver must not break the operation.
                _logger.LogWarning(ex, "Webhook delivery failed. Id: {Id}, Url: {Url}", hook.Id, hook.Url);
            }
        }
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
}
