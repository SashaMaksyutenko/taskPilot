namespace Taskpilot.Integrations;

/// <summary>
/// Email delivery settings (SendGrid), bound from configuration (section "Email").
/// Secrets come from .env / User Secrets — never hard-coded.
/// </summary>
public class EmailOptions
{
    /// <summary>SendGrid API key. Empty disables the SendGrid sender.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Sender address shown to recipients.</summary>
    public string FromEmail { get; set; } = "no-reply@taskpilot.local";

    /// <summary>Sender display name.</summary>
    public string FromName { get; set; } = "TaskPilot";

    /// <summary>Base URL of the frontend, used to turn relative links into clickable ones.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    // --- SMTP (Gmail, Brevo, Mailtrap, …). Preferred when SmtpHost is set. ---

    /// <summary>SMTP server host (e.g. smtp.gmail.com). Empty disables the SMTP sender.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>SMTP port (587 = STARTTLS, the common default).</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>SMTP username (usually the full email address).</summary>
    public string SmtpUser { get; set; } = string.Empty;

    /// <summary>SMTP password or app password.</summary>
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>Use STARTTLS/SSL (true for port 587/465).</summary>
    public bool SmtpUseSsl { get; set; } = true;

    /// <summary>True when SMTP delivery is configured.</summary>
    public bool SmtpConfigured => !string.IsNullOrWhiteSpace(SmtpHost);

    /// <summary>True when the SendGrid API sender is configured.</summary>
    public bool SendGridConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
