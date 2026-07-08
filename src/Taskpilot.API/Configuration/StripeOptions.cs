namespace Taskpilot.API.Configuration;

/// <summary>
/// Stripe payment settings, bound from the "Stripe" configuration section
/// (populated from .env: Stripe__SecretKey, Stripe__Currency). The secret key is
/// kept out of source control — payments stay disabled until it is provided.
/// </summary>
public class StripeOptions
{
    /// <summary>Stripe secret API key (sk_test_… / sk_live_…). Empty = payments disabled.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>ISO currency code used for checkout (defaults to USD).</summary>
    public string Currency { get; set; } = "usd";

    /// <summary>True only when a secret key is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}
