using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using Taskpilot.API.Configuration;

namespace Taskpilot.API.Services;

/// <summary>
/// Sends email via SendGrid. When no API key is configured the sender is disabled
/// and every call is a safe no-op, so the rest of the app works without email set up.
/// </summary>
public class SendGridEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(IOptions<EmailOptions> options, ILogger<SendGridEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.SendGridConfigured;

    /// <inheritdoc />
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, EmailAttachment? attachment = null)
    {
        if (!IsEnabled)
            return; // email delivery not configured — do nothing

        try
        {
            var client = new SendGridClient(_options.ApiKey);
            var from = new EmailAddress(_options.FromEmail, _options.FromName);
            var to = new EmailAddress(toEmail, toName);
            // Provide a plain-text fallback derived from the subject.
            var msg = MailHelper.CreateSingleEmail(from, to, subject, subject, htmlBody);

            // SendGrid takes attachment bytes base64-encoded.
            if (attachment is not null)
                msg.AddAttachment(
                    attachment.FileName,
                    Convert.ToBase64String(attachment.Content),
                    attachment.ContentType);

            var response = await client.SendEmailAsync(msg);
            if ((int)response.StatusCode >= 400)
                _logger.LogWarning("SendGrid returned {Status} sending to {Email}.", response.StatusCode, toEmail);
        }
        catch (Exception ex)
        {
            // Email is best-effort — never let a delivery failure break the caller.
            _logger.LogError(ex, "Failed to send email to {Email}.", toEmail);
        }
    }
}
