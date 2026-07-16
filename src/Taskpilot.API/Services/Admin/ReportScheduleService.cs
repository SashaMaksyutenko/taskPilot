using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Reports;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Recurring report emails: the user picks a report, a format and a cadence, and the
/// background worker generates it and mails it whenever the cadence has elapsed.
/// </summary>
public class ReportScheduleService : IReportScheduleService
{
    // Keep the list manageable per user and project.
    private const int MaxPerUserPerProject = 5;

    private readonly TaskpilotDbContext _context;
    private readonly IReportService _reports;
    private readonly IEmailSender _email;
    private readonly ILogger<ReportScheduleService> _logger;

    public ReportScheduleService(
        TaskpilotDbContext context,
        IReportService reports,
        IEmailSender email,
        ILogger<ReportScheduleService> logger)
    {
        _context = context;
        _reports = reports;
        _email = email;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ReportScheduleDto>> CreateAsync(Guid userId, Guid projectId, CreateReportScheduleDto dto)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<ReportScheduleDto>.Fail("Project not found.");

        if (!Enum.TryParse<ReportKind>(dto.Kind, ignoreCase: true, out var kind))
            return Result<ReportScheduleDto>.Fail("Invalid report kind.");
        if (!Enum.TryParse<ReportFormat>(dto.Format, ignoreCase: true, out var format))
            return Result<ReportScheduleDto>.Fail("Invalid report format.");
        if (!Enum.TryParse<ReportFrequency>(dto.Frequency, ignoreCase: true, out var frequency))
            return Result<ReportScheduleDto>.Fail("Invalid report frequency.");

        var count = await _context.ReportSchedules.CountAsync(s => s.UserId == userId && s.ProjectId == projectId);
        if (count >= MaxPerUserPerProject)
            return Result<ReportScheduleDto>.Fail($"You can schedule at most {MaxPerUserPerProject} reports per project.");

        // The same report/format/cadence twice would just mail duplicates.
        var duplicate = await _context.ReportSchedules.AnyAsync(s =>
            s.UserId == userId && s.ProjectId == projectId &&
            s.Kind == kind && s.Format == format && s.Frequency == frequency);
        if (duplicate)
            return Result<ReportScheduleDto>.Fail("That report is already scheduled.");

        var schedule = new ReportSchedule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProjectId = projectId,
            Kind = kind,
            Format = format,
            Frequency = frequency,
            CreatedAt = DateTime.UtcNow,
        };
        _context.ReportSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Report scheduled. UserId: {UserId}, ProjectId: {ProjectId}, Kind: {Kind}, Frequency: {Frequency}",
            userId, projectId, kind, frequency);
        return Result<ReportScheduleDto>.Ok(Map(schedule));
    }

    /// <inheritdoc />
    public async Task<Result<List<ReportScheduleDto>>> GetForProjectAsync(Guid userId, Guid projectId)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<List<ReportScheduleDto>>.Fail("Project not found.");

        var items = await _context.ReportSchedules
            .Where(s => s.UserId == userId && s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<ReportScheduleDto>>.Ok(items.Select(Map).ToList());
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid userId, Guid scheduleId)
    {
        var schedule = await _context.ReportSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.UserId == userId);
        if (schedule is null)
            return Result.Fail("Schedule not found.");

        _context.ReportSchedules.Remove(schedule);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<int> SendDueAsync()
    {
        // Nothing to do if email delivery is not configured.
        if (!_email.IsEnabled)
            return 0;

        var now = DateTime.UtcNow;
        var schedules = await _context.ReportSchedules
            .Include(s => s.User)
            .Include(s => s.Project)
            .ToListAsync();

        var sent = 0;
        foreach (var schedule in schedules)
        {
            if (!IsDue(schedule.Frequency, schedule.LastSentAt, now))
                continue;

            // The user may have lost access since scheduling; drop the schedule then.
            if (!await ProjectAccess.CanAccessAsync(_context, schedule.ProjectId, schedule.UserId))
            {
                _context.ReportSchedules.Remove(schedule);
                _logger.LogInformation("Report schedule removed (access lost). Id: {Id}", schedule.Id);
                continue;
            }

            var report = await GenerateAsync(schedule);
            if (report is null)
                continue;

            await _email.SendAsync(
                schedule.User.Email,
                schedule.User.Name,
                $"{schedule.Kind} report — {schedule.Project.Name}",
                $"<p>Your {schedule.Frequency.ToString().ToLowerInvariant()} <strong>{schedule.Kind}</strong> report for " +
                $"\"{schedule.Project.Name}\" is attached.</p>" +
                "<p style=\"color:#6b7280;font-size:12px\">You receive this because you scheduled it in TaskPilot.</p>",
                report);

            schedule.LastSentAt = now;
            sent++;
        }

        await _context.SaveChangesAsync();
        if (sent > 0)
            _logger.LogInformation("Scheduled reports sent: {Count}.", sent);
        return sent;
    }

    /// <summary>Builds the report bytes for a schedule, or null when generation fails.</summary>
    private async Task<EmailAttachment?> GenerateAsync(ReportSchedule schedule)
    {
        var isPdf = schedule.Format == ReportFormat.Pdf;

        var result = (schedule.Kind, isPdf) switch
        {
            (ReportKind.Project, true) => await _reports.ProjectReportPdfAsync(schedule.UserId, schedule.ProjectId),
            (ReportKind.Project, false) => await _reports.ProjectReportXlsxAsync(schedule.UserId, schedule.ProjectId),
            (ReportKind.Team, true) => await _reports.TeamReportPdfAsync(schedule.UserId, schedule.ProjectId),
            _ => await _reports.TeamReportXlsxAsync(schedule.UserId, schedule.ProjectId),
        };

        if (!result.Succeeded)
        {
            _logger.LogWarning("Scheduled report generation failed. Id: {Id}, Error: {Error}", schedule.Id, result.Error);
            return null;
        }

        var name = $"{schedule.Kind.ToString().ToLowerInvariant()}-report-{DateTime.UtcNow:yyyy-MM-dd}";
        return isPdf
            ? new EmailAttachment($"{name}.pdf", "application/pdf", result.Value!)
            : new EmailAttachment(
                $"{name}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                result.Value!);
    }

    /// <summary>Whether the cadence has elapsed since the last send.</summary>
    private static bool IsDue(ReportFrequency frequency, DateTime? lastSent, DateTime now)
    {
        if (lastSent is null)
            return true;

        var elapsed = now - lastSent.Value;
        return frequency switch
        {
            ReportFrequency.Daily => elapsed >= TimeSpan.FromHours(24),
            ReportFrequency.Weekly => elapsed >= TimeSpan.FromDays(7),
            ReportFrequency.Monthly => elapsed >= TimeSpan.FromDays(30),
            _ => false,
        };
    }

    private static ReportScheduleDto Map(ReportSchedule s) => new()
    {
        Id = s.Id,
        ProjectId = s.ProjectId,
        Kind = s.Kind.ToString(),
        Format = s.Format.ToString(),
        Frequency = s.Frequency.ToString(),
        LastSentAt = s.LastSentAt,
        CreatedAt = s.CreatedAt,
    };
}
