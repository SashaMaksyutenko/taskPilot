namespace Taskpilot.API.Configuration;

/// <summary>
/// VAPID keys for Web Push, bound from configuration (section "Vapid"). Generate a
/// key pair once and keep the private key out of source control. Empty = push disabled.
/// </summary>
public class VapidOptions
{
    /// <summary>Contact subject — a "mailto:" address or site URL.</summary>
    public string Subject { get; set; } = "mailto:sasha.maksyutenko@gmail.com";

    /// <summary>Public VAPID key (safe to expose to the browser).</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Private VAPID key (secret).</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>True only when both keys are configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}
