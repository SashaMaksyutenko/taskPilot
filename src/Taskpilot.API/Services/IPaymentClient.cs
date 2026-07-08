using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>A created Stripe Checkout session: its id and the hosted payment page URL.</summary>
public record CheckoutSession(string Id, string Url);

/// <summary>
/// Talks to Stripe's Checkout API. The real implementation posts to Stripe; tests
/// provide a stub so payment flow can be verified without any network calls.
/// </summary>
public interface IPaymentClient
{
    /// <summary>True only when a Stripe secret key is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Creates a hosted Checkout session for a one-off payment of <paramref name="amount"/>
    /// (major units, e.g. dollars), returning the session id and the redirect URL.
    /// </summary>
    Task<Result<CheckoutSession>> CreateCheckoutSessionAsync(
        decimal amount, string description, string successUrl, string cancelUrl);

    /// <summary>Returns true when the given Checkout session has been paid.</summary>
    Task<Result<bool>> IsSessionPaidAsync(string sessionId);
}
