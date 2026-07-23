namespace Taskpilot.Integrations;

/// <summary>A file to attach to an outgoing email (e.g. a generated report).</summary>
/// <param name="FileName">Name the recipient sees (e.g. "team-report.pdf").</param>
/// <param name="ContentType">MIME type of the bytes.</param>
/// <param name="Content">The file's bytes.</param>
public record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Sends transactional emails. Implementations are no-ops when email delivery is
/// not configured, so callers never need to check first.
/// </summary>
public interface IEmailSender
{
    /// <summary>True when an email provider (API key) is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Sends an HTML email, optionally with one file attached; does nothing (and never
    /// throws) when disabled.
    /// </summary>
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, EmailAttachment? attachment = null);
}
