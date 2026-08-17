using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <summary>
/// Real Stripe subscription/billing client over Stripe's REST API (form-encoded, Bearer secret key),
/// matching the no-SDK style of <see cref="StripePaymentClient"/>.
/// </summary>
public class StripeBillingClient : IStripeBillingClient
{
    private const string CheckoutEndpoint = "https://api.stripe.com/v1/checkout/sessions";
    private const string PortalEndpoint = "https://api.stripe.com/v1/billing_portal/sessions";
    private const string SubscriptionEndpoint = "https://api.stripe.com/v1/subscriptions";

    private readonly HttpClient _http;
    private readonly StripeOptions _options;
    private readonly ILogger<StripeBillingClient> _logger;

    public StripeBillingClient(HttpClient http, IOptions<StripeOptions> options, ILogger<StripeBillingClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.SubscriptionsConfigured;

    /// <inheritdoc />
    public async Task<Result<string>> CreateSubscriptionCheckoutAsync(string? customerId, string? customerEmail, string successUrl, string cancelUrl)
    {
        if (!_options.SubscriptionsConfigured)
            return Result<string>.Fail("Subscriptions are not configured.");

        var form = new Dictionary<string, string>
        {
            ["mode"] = "subscription",
            ["success_url"] = successUrl,
            ["cancel_url"] = cancelUrl,
            ["line_items[0][price]"] = _options.ProPriceId,
            ["line_items[0][quantity]"] = "1",
        };
        // Reuse an existing customer if we have one, else let Stripe create one from the email.
        if (!string.IsNullOrEmpty(customerId)) form["customer"] = customerId;
        else if (!string.IsNullOrEmpty(customerEmail)) form["customer_email"] = customerEmail;

        return await PostForUrlAsync(CheckoutEndpoint, form, "start the subscription");
    }

    /// <inheritdoc />
    public async Task<Result<string>> CreatePortalSessionAsync(string customerId, string returnUrl)
    {
        if (!_options.IsConfigured)
            return Result<string>.Fail("Billing is not configured.");

        var form = new Dictionary<string, string> { ["customer"] = customerId, ["return_url"] = returnUrl };
        return await PostForUrlAsync(PortalEndpoint, form, "open the billing portal");
    }

    /// <inheritdoc />
    public async Task<Result<(bool active, DateTime? renewsAt)>> GetSubscriptionAsync(string subscriptionId)
    {
        if (!_options.IsConfigured)
            return Result<(bool, DateTime?)>.Fail("Billing is not configured.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{SubscriptionEndpoint}/{subscriptionId}");
            Authorize(request);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return Result<(bool, DateTime?)>.Fail("Could not read the subscription.");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            var active = status is "active" or "trialing";
            DateTime? renewsAt = root.TryGetProperty("current_period_end", out var e) && e.TryGetInt64(out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime
                : null;
            return Result<(bool, DateTime?)>.Ok((active, renewsAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Stripe subscription.");
            return Result<(bool, DateTime?)>.Fail("Could not read the subscription.");
        }
    }

    private async Task<Result<string>> PostForUrlAsync(string endpoint, Dictionary<string, string> form, string action)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new FormUrlEncodedContent(form) };
            Authorize(request);
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stripe {Action} failed. Status: {Status}", action, response.StatusCode);
                return Result<string>.Fail($"Could not {action}.");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var url = doc.RootElement.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
            return string.IsNullOrEmpty(url) ? Result<string>.Fail($"Could not {action}.") : Result<string>.Ok(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Stripe to {Action}.", action);
            return Result<string>.Fail($"Could not {action}.");
        }
    }

    private void Authorize(HttpRequestMessage request) =>
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);
}
