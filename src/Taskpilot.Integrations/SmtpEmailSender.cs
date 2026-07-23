using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Taskpilot.Integrations;

/// <summary>
/// Sends email over SMTP (Gmail, Brevo, Mailtrap, …). Disabled and a safe no-op
/// when no SMTP host is configured, so the app runs fine without email set up.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.SmtpConfigured;

    /// <inheritdoc />
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, EmailAttachment? attachment = null)
    {
        if (!IsEnabled)
            return; // SMTP not configured — do nothing

        try
        {
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.SmtpUseSsl,
                Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPassword),
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(_options.FromEmail, _options.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
            };
            mail.To.Add(new MailAddress(toEmail, toName));

            // The stream must outlive SendMailAsync, so dispose it after the send.
            using var attachmentStream = attachment is null ? null : new MemoryStream(attachment.Content);
            if (attachment is not null)
                mail.Attachments.Add(new Attachment(attachmentStream!, attachment.FileName, attachment.ContentType));

            await client.SendMailAsync(mail);
            _logger.LogInformation("Email sent to {Email} via SMTP.", toEmail);
        }
        catch (Exception ex)
        {
            // Email is best-effort — never let a delivery failure break the caller.
            _logger.LogError(ex, "Failed to send email to {Email} via SMTP.", toEmail);
        }
    }
}
