using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>Generates analytical project reports (summary + per-member breakdown).</summary>
public interface IReportService
{
    /// <summary>Renders a project health/performance report as a PDF.</summary>
    Task<Result<byte[]>> ProjectReportPdfAsync(Guid userId, Guid projectId);

    /// <summary>Renders a project health/performance report as an Excel workbook.</summary>
    Task<Result<byte[]>> ProjectReportXlsxAsync(Guid userId, Guid projectId);

    /// <summary>
    /// Renders a team-performance report as a PDF: per participant, their completion
    /// rate, on-time rate, overdue count and reputation.
    /// </summary>
    Task<Result<byte[]>> TeamReportPdfAsync(Guid userId, Guid projectId);

    /// <summary>Renders the team-performance report as an Excel workbook.</summary>
    Task<Result<byte[]>> TeamReportXlsxAsync(Guid userId, Guid projectId);

    /// <summary>
    /// Renders an organisation-wide marketplace report as a PDF (tasks by status,
    /// applications, completion and payment totals, top freelancers). Admins only.
    /// </summary>
    Task<Result<byte[]>> MarketplaceReportPdfAsync(Guid userId);

    /// <summary>Renders the marketplace report as an Excel workbook. Admins only.</summary>
    Task<Result<byte[]>> MarketplaceReportXlsxAsync(Guid userId);

    /// <summary>Renders the audit log as a PDF (most recent entries first). Admins only.</summary>
    Task<Result<byte[]>> AuditReportPdfAsync(Guid userId);

    /// <summary>Renders the audit log as an Excel workbook. Admins only.</summary>
    Task<Result<byte[]>> AuditReportXlsxAsync(Guid userId);

    /// <summary>
    /// Renders a user's activity report as a PDF (logins, tasks, chat, forum,
    /// marketplace, reputation). Users may run it for themselves; admins for anyone.
    /// </summary>
    Task<Result<byte[]>> UserActivityReportPdfAsync(Guid callerId, Guid targetUserId);

    /// <summary>Renders the user activity report as an Excel workbook.</summary>
    Task<Result<byte[]>> UserActivityReportXlsxAsync(Guid callerId, Guid targetUserId);
}
