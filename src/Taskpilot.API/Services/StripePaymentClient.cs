using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <summary>
/// Real Stripe Checkout client. Creates a hosted checkout session and reads its
/// payment status via Stripe's REST API (form-encoded requests, Bearer secret key)
/// — no SDK dependency, matching the other integration clients.
/// </summary>
public class StripePaymentClient : IPaymentClient
{
    private const string SessionsEndpoint = "https://api.stripe.com/v1/checkout/sessions";

    private readonly HttpClient _http;
    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentClient> _logger;

    public StripePaymentClient(HttpClient http, IOptions<StripeOptions> options, ILogger<StripePaymentClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.IsConfigured;

    /// <inheritdoc />
    public async Task<Result<CheckoutSession>> CreateCheckoutSessionAsync(
        decimal amount, string description, string successUrl, string cancelUrl)
    {
        if (!_options.IsConfigured)
            return Result<CheckoutSession>.Fail("Payments are not configured.");

        // Stripe expects the amount in the currency's smallest unit (e.g. cents).
        var unitAmount = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["success_url"] = successUrl,
            ["cancel_url"] = cancelUrl,
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = _options.Currency,
            ["line_items[0][price_data][unit_amount]"] = unitAmount.ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = description,
        });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SessionsEndpoint) { Content = form };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stripe session create failed. Status: {Status}", response.StatusCode);
                return Result<CheckoutSession>.Fail("Could not start the payment.");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var url = root.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(url))
                return Result<CheckoutSession>.Fail("Could not start the payment.");

            return Result<CheckoutSession>.Ok(new CheckoutSession(id, url));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Stripe checkout session.");
            return Result<CheckoutSession>.Fail("Could not start the payment.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> IsSessionPaidAsync(string sessionId)
    {
        if (!_options.IsConfigured)
            return Result<bool>.Fail("Payments are not configured.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{SessionsEndpoint}/{sessionId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stripe session fetch failed. Status: {Status}", response.StatusCode);
                return Result<bool>.Fail("Could not verify the payment.");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var status = doc.RootElement.TryGetProperty("payment_status", out var el) ? el.GetString() : null;
            return Result<bool>.Ok(status == "paid");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Stripe checkout session.");
            return Result<bool>.Fail("Could not verify the payment.");
        }
    }
}
