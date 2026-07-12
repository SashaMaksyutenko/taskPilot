using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>Generates analytical project reports (summary + per-member breakdown).</summary>
public interface IReportService
{
    /// <summary>Renders a project health/performance report as a PDF.</summary>
    Task<Result<byte[]>> ProjectReportPdfAsync(Guid userId, Guid projectId);

    /// <summary>Renders a project health/performance report as an Excel workbook.</summary>
    Task<Result<byte[]>> ProjectReportXlsxAsync(Guid userId, Guid projectId);
}
