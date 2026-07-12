using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Downloadable analytical project reports (PDF / Excel). Require authentication.</summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/report")]
public class ReportsController : BaseApiController
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
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
}
