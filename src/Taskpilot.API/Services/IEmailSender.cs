namespace Taskpilot.API.Services;

/// <summary>
/// Sends transactional emails. Implementations are no-ops when email delivery is
/// not configured, so callers never need to check first.
/// </summary>
public interface IEmailSender
{
    /// <summary>True when an email provider (API key) is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>Sends an HTML email; does nothing (and never throws) when disabled.</summary>
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
}
