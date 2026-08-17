using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>
/// Stripe calls for recurring subscriptions: start a subscription Checkout and open the customer
/// billing portal. Separate from the one-time <see cref="IPaymentClient"/> used by the marketplace.
/// </summary>
public interface IStripeBillingClient
{
    /// <summary>True when a secret key and a Pro price id are configured.</summary>
    bool IsEnabled { get; }

    /// <summary>Creates a subscription Checkout session for the Pro price; returns its hosted URL.</summary>
    Task<Result<string>> CreateSubscriptionCheckoutAsync(string? customerId, string? customerEmail, string successUrl, string cancelUrl);

    /// <summary>Creates a billing-portal session so the customer can manage or cancel; returns its URL.</summary>
    Task<Result<string>> CreatePortalSessionAsync(string customerId, string returnUrl);

    /// <summary>Reads a subscription's period-end and whether it's still active.</summary>
    Task<Result<(bool active, DateTime? renewsAt)>> GetSubscriptionAsync(string subscriptionId);
}
