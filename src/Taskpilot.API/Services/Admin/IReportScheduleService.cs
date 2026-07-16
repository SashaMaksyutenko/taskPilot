using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Reports;

namespace Taskpilot.API.Services;

/// <summary>Manages recurring report emails and sends the ones that are due.</summary>
public interface IReportScheduleService
{
    /// <summary>Schedules a recurring report for a project the user can access.</summary>
    Task<Result<ReportScheduleDto>> CreateAsync(Guid userId, Guid projectId, CreateReportScheduleDto dto);

    /// <summary>Lists the user's schedules for a project (newest first).</summary>
    Task<Result<List<ReportScheduleDto>>> GetForProjectAsync(Guid userId, Guid projectId);

    /// <summary>Deletes one of the user's schedules.</summary>
    Task<Result> DeleteAsync(Guid userId, Guid scheduleId);

    /// <summary>
    /// Generates and emails every schedule whose cadence has elapsed. Returns how many
    /// were sent. A no-op when email delivery is not configured.
    /// </summary>
    Task<int> SendDueAsync();
}
