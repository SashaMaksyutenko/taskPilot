using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Organisation-wide reports. These span everyone's data, so they are admin-only
/// (the service re-checks the role as well).
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/reports")]
public class AdminReportsController : BaseApiController
{
    private readonly IReportService _reports;

    public AdminReportsController(IReportService reports)
    {
        _reports = reports;
    }

    /// <summary>Downloads the marketplace report as a PDF.</summary>
    [HttpGet("marketplace/pdf")]
    public async Task<IActionResult> MarketplacePdf()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _reports.MarketplaceReportPdfAsync(userId.Value);
        if (!result.Succeeded) return Forbid();
        return File(result.Value!, "application/pdf", "marketplace-report.pdf");
    }

    /// <summary>Downloads the marketplace report as an Excel (.xlsx) workbook.</summary>
    [HttpGet("marketplace/xlsx")]
    public async Task<IActionResult> MarketplaceXlsx()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _reports.MarketplaceReportXlsxAsync(userId.Value);
        if (!result.Succeeded) return Forbid();
        return File(
            result.Value!,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "marketplace-report.xlsx");
    }

    /// <summary>Downloads the audit log (most recent entries) as a PDF.</summary>
    [HttpGet("audit/pdf")]
    public async Task<IActionResult> AuditPdf()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _reports.AuditReportPdfAsync(userId.Value);
        if (!result.Succeeded) return Forbid();
        return File(result.Value!, "application/pdf", "audit-log.pdf");
    }

    /// <summary>Downloads the audit log as an Excel (.xlsx) workbook.</summary>
    [HttpGet("audit/xlsx")]
    public async Task<IActionResult> AuditXlsx()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _reports.AuditReportXlsxAsync(userId.Value);
        if (!result.Succeeded) return Forbid();
        return File(
            result.Value!,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "audit-log.xlsx");
    }
}
