using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Builds a per-user task summary and emails it on the user's chosen cadence
/// (daily/weekly). Users with nothing to report are skipped so we never send an
/// empty digest.
/// </summary>
public class DigestService : IDigestService
{
    private readonly TaskpilotDbContext _context;
    private readonly IEmailSender _email;
    private readonly ILogger<DigestService> _logger;

    // How soon a deadline counts as "due soon" in the digest.
    private static readonly TimeSpan DueSoonWindow = TimeSpan.FromDays(7);

    public DigestService(TaskpilotDbContext context, IEmailSender email, ILogger<DigestService> logger)
    {
        _context = context;
        _email = email;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> SendDueDigestsAsync()
    {
        // Nothing to do if email delivery is not configured.
        if (!_email.IsEnabled)
            return 0;

        var now = DateTime.UtcNow;

        // Candidates: opted-in users whose cadence has elapsed since the last send.
        var candidates = await _context.Users
            .Where(u => u.IsActive && u.DigestFrequency != DigestFrequency.Off)
            .Select(u => new { u.Id, u.Name, u.Email, u.DigestFrequency, u.LastDigestSentAt })
            .ToListAsync();

        var sent = 0;
        foreach (var user in candidates)
        {
            if (!IsDue(user.DigestFrequency, user.LastDigestSentAt, now))
                continue;

            // Tasks assigned to this user that are still open (not Done).
            var openTasks = await _context.ProjectTasks
                .Include(t => t.Project)
                .Where(t => t.AssigneeId == user.Id && t.Status != ProjectTaskStatus.Done)
                .AsNoTracking()
                .ToListAsync();

            var overdue = openTasks.Where(t => t.Deadline != null && t.Deadline < now).ToList();
            var dueSoon = openTasks
                .Where(t => t.Deadline != null && t.Deadline >= now && t.Deadline <= now + DueSoonWindow)
                .OrderBy(t => t.Deadline)
                .ToList();

            // Skip users with nothing worth an email.
            if (openTasks.Count == 0)
            {
                await StampSentAsync(user.Id, now);
                continue;
            }

            var html = BuildHtml(user.Name, openTasks.Count, overdue, dueSoon);
            var subject = overdue.Count > 0
                ? $"Your Taskpilot digest — {overdue.Count} overdue"
                : "Your Taskpilot digest";

            await _email.SendAsync(user.Email, user.Name, subject, html);
            await StampSentAsync(user.Id, now);
            sent++;
        }

        if (sent > 0)
            _logger.LogInformation("Digest run sent {Count} email(s).", sent);
        return sent;
    }

    /// <summary>Whether the cadence has elapsed since the last send.</summary>
    private static bool IsDue(DigestFrequency frequency, DateTime? lastSent, DateTime now)
    {
        if (lastSent is null)
            return true;

        var elapsed = now - lastSent.Value;
        return frequency switch
        {
            DigestFrequency.Daily => elapsed >= TimeSpan.FromHours(24),
            DigestFrequency.Weekly => elapsed >= TimeSpan.FromDays(7),
            _ => false,
        };
    }

    /// <summary>Records the last-sent time for the user.</summary>
    private async Task StampSentAsync(Guid userId, DateTime now)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return;
        user.LastDigestSentAt = now;
        await _context.SaveChangesAsync();
    }

    /// <summary>Renders the digest body as simple, email-client-safe HTML.</summary>
    private static string BuildHtml(string name, int openCount, List<ProjectTask> overdue, List<ProjectTask> dueSoon)
    {
        var sb = new StringBuilder();
        sb.Append($"<h2>Hi {WebUtility.HtmlEncode(name)},</h2>");
        sb.Append($"<p>You have <strong>{openCount}</strong> open task(s).</p>");

        if (overdue.Count > 0)
        {
            sb.Append($"<h3 style=\"color:#dc2626\">Overdue ({overdue.Count})</h3><ul>");
            foreach (var t in overdue)
                sb.Append($"<li>{WebUtility.HtmlEncode(t.Title)} — {WebUtility.HtmlEncode(t.Project.Name)} (due {t.Deadline:yyyy-MM-dd})</li>");
            sb.Append("</ul>");
        }

        if (dueSoon.Count > 0)
        {
            sb.Append($"<h3>Due this week ({dueSoon.Count})</h3><ul>");
            foreach (var t in dueSoon)
                sb.Append($"<li>{WebUtility.HtmlEncode(t.Title)} — {WebUtility.HtmlEncode(t.Project.Name)} (due {t.Deadline:yyyy-MM-dd})</li>");
            sb.Append("</ul>");
        }

        sb.Append("<p style=\"color:#6b7280;font-size:12px\">You receive this because you enabled digest emails in Taskpilot settings.</p>");
        return sb.ToString();
    }
}
