using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Builds analytical project reports: task status breakdown, completion rate,
/// overdue count and a per-member summary, rendered as PDF or Excel.
/// </summary>
public class ReportService : IReportService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<ReportService> _logger;

    // The board's status columns, in workflow order.
    private static readonly ProjectTaskStatus[] StatusOrder =
        { ProjectTaskStatus.Backlog, ProjectTaskStatus.InProgress, ProjectTaskStatus.Review, ProjectTaskStatus.Done };

    public ReportService(TaskpilotDbContext context, ILogger<ReportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>A member's row in the report.</summary>
    private sealed record MemberRow(string Name, int Assigned, int Done, int Overdue);

    /// <summary>Everything the report needs, gathered once.</summary>
    private sealed record ReportData(
        string ProjectName,
        int Total,
        Dictionary<ProjectTaskStatus, int> ByStatus,
        int Overdue,
        int CompletionPct,
        List<MemberRow> Members);

    /// <summary>Loads and aggregates the report data (or null if no access).</summary>
    private async Task<ReportData?> GatherAsync(Guid userId, Guid projectId)
    {
        // Access check: owner or member.
        var project = await _context.Projects
            .Where(p => p.Id == projectId && (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)))
            .Select(p => new { p.Name })
            .FirstOrDefaultAsync();
        if (project is null)
            return null;

        var tasks = await _context.ProjectTasks
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.Assignee)
            .AsNoTracking()
            .ToListAsync();

        var now = DateTime.UtcNow;
        bool IsOverdue(ProjectTask t) => t.Status != ProjectTaskStatus.Done && t.Deadline != null && t.Deadline < now;

        var byStatus = StatusOrder.ToDictionary(s => s, s => tasks.Count(t => t.Status == s));
        var total = tasks.Count;
        var done = byStatus[ProjectTaskStatus.Done];
        var overdue = tasks.Count(IsOverdue);
        var completionPct = total > 0 ? (int)Math.Round(done * 100.0 / total) : 0;

        // Per-assignee breakdown (unassigned tasks grouped under "Unassigned").
        var members = tasks
            .GroupBy(t => t.Assignee?.Name ?? "Unassigned")
            .Select(g => new MemberRow(
                g.Key,
                g.Count(),
                g.Count(t => t.Status == ProjectTaskStatus.Done),
                g.Count(IsOverdue)))
            .OrderByDescending(m => m.Assigned)
            .ToList();

        return new ReportData(project.Name, total, byStatus, overdue, completionPct, members);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ProjectReportPdfAsync(Guid userId, Guid projectId)
    {
        var data = await GatherAsync(userId, projectId);
        if (data is null)
            return Result<byte[]>.Fail("Project not found.");

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text($"Project report — {data.ProjectName}").FontSize(18).Bold();
                    col.Item().Text($"Generated {DateTime.UtcNow:u}").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    // Summary line.
                    col.Item().Text($"Total tasks: {data.Total}   ·   Completed: {data.CompletionPct}%   ·   Overdue: {data.Overdue}")
                        .FontSize(11).SemiBold();

                    // Status breakdown table.
                    col.Item().PaddingTop(10).Text("Tasks by status").Bold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Status").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Count").Bold();
                        });
                        foreach (var s in StatusOrder)
                        {
                            table.Cell().Padding(4).Text(s.ToString());
                            table.Cell().Padding(4).Text(data.ByStatus[s].ToString());
                        }
                    });

                    // Per-member table.
                    col.Item().PaddingTop(14).Text("By member").Bold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        table.Header(h =>
                        {
                            foreach (var head in new[] { "Member", "Assigned", "Done", "Overdue" })
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(head).Bold();
                        });
                        foreach (var m in data.Members)
                        {
                            table.Cell().Padding(4).Text(m.Name);
                            table.Cell().Padding(4).Text(m.Assigned.ToString());
                            table.Cell().Padding(4).Text(m.Done.ToString());
                            table.Cell().Padding(4).Text(m.Overdue.ToString());
                        }
                    });
                });
            });
        }).GeneratePdf();

        _logger.LogInformation("Project report PDF generated. ProjectId: {ProjectId}, By: {UserId}", projectId, userId);
        return Result<byte[]>.Ok(pdf);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ProjectReportXlsxAsync(Guid userId, Guid projectId)
    {
        var data = await GatherAsync(userId, projectId);
        if (data is null)
            return Result<byte[]>.Fail("Project not found.");

        using var workbook = new XLWorkbook();

        // Summary sheet.
        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = "Project";
        summary.Cell(1, 2).Value = data.ProjectName;
        summary.Cell(2, 1).Value = "Generated (UTC)";
        summary.Cell(2, 2).Value = DateTime.UtcNow.ToString("u");
        summary.Cell(3, 1).Value = "Total tasks";
        summary.Cell(3, 2).Value = data.Total;
        summary.Cell(4, 1).Value = "Completion %";
        summary.Cell(4, 2).Value = data.CompletionPct;
        summary.Cell(5, 1).Value = "Overdue";
        summary.Cell(5, 2).Value = data.Overdue;

        var r = 7;
        summary.Cell(r, 1).Value = "Status";
        summary.Cell(r, 2).Value = "Count";
        summary.Row(r).Style.Font.Bold = true;
        foreach (var s in StatusOrder)
        {
            r++;
            summary.Cell(r, 1).Value = s.ToString();
            summary.Cell(r, 2).Value = data.ByStatus[s];
        }
        summary.Columns().AdjustToContents();

        // Members sheet.
        var members = workbook.Worksheets.Add("Members");
        string[] headers = { "Member", "Assigned", "Done", "Overdue" };
        for (var c = 0; c < headers.Length; c++)
            members.Cell(1, c + 1).Value = headers[c];
        members.Row(1).Style.Font.Bold = true;
        var row = 2;
        foreach (var m in data.Members)
        {
            members.Cell(row, 1).Value = m.Name;
            members.Cell(row, 2).Value = m.Assigned;
            members.Cell(row, 3).Value = m.Done;
            members.Cell(row, 4).Value = m.Overdue;
            row++;
        }
        members.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        _logger.LogInformation("Project report XLSX generated. ProjectId: {ProjectId}, By: {UserId}", projectId, userId);
        return Result<byte[]>.Ok(stream.ToArray());
    }
}
