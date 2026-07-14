using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Reports;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Downloadable analytical project reports (PDF / Excel). Require authentication.</summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/report")]
public class ReportsController : BaseApiController
{
    private readonly IReportService _reports;
    private readonly IReportScheduleService _schedules;

    public ReportsController(IReportService reports, IReportScheduleService schedules)
    {
        _reports = reports;
        _schedules = schedules;
    }

    /// <summary>Lists the current user's scheduled reports for this project.</summary>
    [HttpGet("schedules")]
    public async Task<IActionResult> GetSchedules(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _schedules.GetForProjectAsync(userId.Value, projectId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Schedules a recurring report email for this project.</summary>
    [HttpPost("schedules")]
    public async Task<IActionResult> CreateSchedule(Guid projectId, [FromBody] CreateReportScheduleDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _schedules.CreateAsync(userId.Value, projectId, dto);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes one of the current user's scheduled reports.</summary>
    [HttpDelete("schedules/{scheduleId:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid projectId, Guid scheduleId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _schedules.DeleteAsync(userId.Value, scheduleId);
        return result.Succeeded ? NoContent() : NotFound(new { error = result.Error });
    }

    /// <summary>Downloads the project report as a PDF.</summary>
    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _reports.ProjectReportPdfAsync(userId.Value, projectId);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return File(result.Value!, "application/pdf", $"project-report-{projectId}.pdf");
    }

    /// <summary>Downloads the project report as an Excel (.xlsx) workbook.</summary>
    [HttpGet("xlsx")]
    public async Task<IActionResult> Xlsx(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _reports.ProjectReportXlsxAsync(userId.Value, projectId);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return File(
            result.Value!,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"project-report-{projectId}.xlsx");
    }

    /// <summary>Downloads the team-performance report as a PDF.</summary>
    [HttpGet("team/pdf")]
    public async Task<IActionResult> TeamPdf(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _reports.TeamReportPdfAsync(userId.Value, projectId);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return File(result.Value!, "application/pdf", $"team-report-{projectId}.pdf");
    }

    /// <summary>Downloads the team-performance report as an Excel (.xlsx) workbook.</summary>
    [HttpGet("team/xlsx")]
    public async Task<IActionResult> TeamXlsx(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _reports.TeamReportXlsxAsync(userId.Value, projectId);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return File(
            result.Value!,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"team-report-{projectId}.xlsx");
    }
}
