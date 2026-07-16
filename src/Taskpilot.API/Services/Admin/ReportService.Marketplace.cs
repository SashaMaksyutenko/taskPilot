using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>Marketplace analytics report (freelancers, earnings, ratings) — PDF/Excel.</summary>
public partial class ReportService
{
    // Marketplace lifecycle, in the order it reads best in a report.
    private static readonly MarketplaceTaskStatus[] MarketStatusOrder =
    {
        MarketplaceTaskStatus.Open, MarketplaceTaskStatus.InProgress, MarketplaceTaskStatus.Submitted,
        MarketplaceTaskStatus.Completed, MarketplaceTaskStatus.Cancelled,
    };

    /// <summary>A freelancer's row in the marketplace report.</summary>
    /// <param name="AvgRating">Null when the freelancer has received no reviews.</param>
    private sealed record FreelancerRow(string Name, int Completed, decimal Earned, double? AvgRating);

    /// <summary>Everything the marketplace report needs.</summary>
    private sealed record MarketplaceReportData(
        int Total,
        Dictionary<MarketplaceTaskStatus, int> ByStatus,
        int CompletionPct,
        int Applications,
        int Accepted,
        int AcceptancePct,
        decimal TotalBudget,
        int PaidTasks,
        decimal PaidAmount,
        double? AvgRating,
        List<FreelancerRow> TopFreelancers)
    {
        /// <summary>"12 (34%)", or just "0" when nobody has applied yet.</summary>
        public string AcceptancePctText() => Applications > 0 ? $"{Accepted} · {AcceptancePct}%" : "0";

        /// <summary>The average rating with a star, or an em dash when there are no reviews.</summary>
        public string AvgRatingText() => AvgRating is { } avg ? $"{avg}★" : "—";
    }

    /// <summary>
    /// Aggregates the organisation's whole marketplace. Null when the caller is not an
    /// admin — this report spans everyone's data, so it is not for regular users.
    /// </summary>
    private async Task<MarketplaceReportData?> GatherMarketplaceAsync(Guid userId)
    {
        if (!await IsAdminAsync(userId))
            return null;

        var tasks = await _context.MarketplaceTasks
            .Include(t => t.Assignee)
            .AsNoTracking()
            .ToListAsync();

        // A projection to a plain enum: nothing is tracked, so no AsNoTracking() here
        // (it only accepts reference types).
        var applications = await _context.TaskApplications
            .Select(a => a.Status)
            .ToListAsync();

        var reviews = await _context.Reviews
            .Select(r => new { r.RateeId, r.Stars })
            .AsNoTracking()
            .ToListAsync();

        var byStatus = MarketStatusOrder.ToDictionary(s => s, s => tasks.Count(t => t.Status == s));
        var total = tasks.Count;
        var completed = byStatus[MarketplaceTaskStatus.Completed];
        var accepted = applications.Count(s => s == ApplicationStatus.Accepted);

        var paid = tasks.Where(t => t.PaymentStatus == PaymentStatus.Paid).ToList();

        // Ratings per freelancer, so the table can show who is trusted.
        var ratingByUser = reviews
            .GroupBy(r => r.RateeId)
            .ToDictionary(g => g.Key, g => g.Average(r => (double)r.Stars));

        // Freelancers ranked by delivered work.
        var topFreelancers = tasks
            .Where(t => t.Status == MarketplaceTaskStatus.Completed && t.AssigneeId != null)
            .GroupBy(t => t.AssigneeId!.Value)
            .Select(g => new FreelancerRow(
                g.First().Assignee?.Name ?? "Unknown",
                g.Count(),
                // Only money that actually changed hands counts as earned.
                g.Where(t => t.PaymentStatus == PaymentStatus.Paid).Sum(t => t.Budget),
                ratingByUser.TryGetValue(g.Key, out var avg) ? Math.Round(avg, 1) : null))
            .OrderByDescending(f => f.Completed)
            .ThenByDescending(f => f.Earned)
            .Take(20)
            .ToList();

        return new MarketplaceReportData(
            total,
            byStatus,
            total > 0 ? (int)Math.Round(completed * 100.0 / total) : 0,
            applications.Count,
            accepted,
            applications.Count > 0 ? (int)Math.Round(accepted * 100.0 / applications.Count) : 0,
            tasks.Sum(t => t.Budget),
            paid.Count,
            paid.Sum(t => t.Budget),
            reviews.Count > 0 ? Math.Round(reviews.Average(r => (double)r.Stars), 1) : null,
            topFreelancers);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> MarketplaceReportPdfAsync(Guid userId)
    {
        var data = await GatherMarketplaceAsync(userId);
        if (data is null)
            return Result<byte[]>.Fail("Only admins can run the marketplace report.");

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("Marketplace report").FontSize(18).Bold();
                    col.Item().Text($"Generated {DateTime.UtcNow:u}").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Item().Text($"Tasks: {data.Total}   ·   Completed: {data.CompletionPct}%   ·   Applications: {data.Applications} (accepted {data.AcceptancePctText()})")
                        .FontSize(11).SemiBold();
                    col.Item().PaddingTop(2).Text($"Budget posted: {data.TotalBudget:0.##}   ·   Paid out: {data.PaidAmount:0.##} across {data.PaidTasks} task(s)   ·   Avg rating: {data.AvgRatingText()}")
                        .FontSize(11).SemiBold();

                    col.Item().PaddingTop(10).Text("Tasks by status").Bold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); });
                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Status").Bold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Count").Bold();
                        });
                        foreach (var s in MarketStatusOrder)
                        {
                            table.Cell().Padding(4).Text(s.ToString());
                            table.Cell().Padding(4).Text(data.ByStatus[s].ToString());
                        }
                    });

                    col.Item().PaddingTop(14).Text("Top freelancers").Bold();
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        table.Header(h =>
                        {
                            foreach (var head in new[] { "Freelancer", "Completed", "Earned", "Rating" })
                                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(head).Bold();
                        });
                        foreach (var f in data.TopFreelancers)
                        {
                            table.Cell().Padding(4).Text(f.Name);
                            table.Cell().Padding(4).Text(f.Completed.ToString());
                            table.Cell().Padding(4).Text($"{f.Earned:0.##}");
                            table.Cell().Padding(4).Text(f.AvgRating is { } r ? $"{r}★" : "—");
                        }
                    });
                });
            });
        }).GeneratePdf();

        _logger.LogInformation("Marketplace report PDF generated. By: {UserId}", userId);
        return Result<byte[]>.Ok(pdf);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> MarketplaceReportXlsxAsync(Guid userId)
    {
        var data = await GatherMarketplaceAsync(userId);
        if (data is null)
            return Result<byte[]>.Fail("Only admins can run the marketplace report.");

        using var workbook = new XLWorkbook();

        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = "Generated (UTC)";
        summary.Cell(1, 2).Value = DateTime.UtcNow.ToString("u");
        summary.Cell(2, 1).Value = "Total tasks";
        summary.Cell(2, 2).Value = data.Total;
        summary.Cell(3, 1).Value = "Completion %";
        summary.Cell(3, 2).Value = data.CompletionPct;
        summary.Cell(4, 1).Value = "Applications";
        summary.Cell(4, 2).Value = data.Applications;
        summary.Cell(5, 1).Value = "Accepted";
        summary.Cell(5, 2).Value = data.Accepted;
        summary.Cell(6, 1).Value = "Acceptance %";
        summary.Cell(6, 2).Value = data.AcceptancePct;
        summary.Cell(7, 1).Value = "Budget posted";
        summary.Cell(7, 2).Value = data.TotalBudget;
        summary.Cell(8, 1).Value = "Paid tasks";
        summary.Cell(8, 2).Value = data.PaidTasks;
        summary.Cell(9, 1).Value = "Paid amount";
        summary.Cell(9, 2).Value = data.PaidAmount;
        summary.Cell(10, 1).Value = "Avg rating";
        if (data.AvgRating is { } avg) summary.Cell(10, 2).Value = avg;
        else summary.Cell(10, 2).Value = "—";

        var r = 12;
        summary.Cell(r, 1).Value = "Status";
        summary.Cell(r, 2).Value = "Count";
        summary.Row(r).Style.Font.Bold = true;
        foreach (var s in MarketStatusOrder)
        {
            r++;
            summary.Cell(r, 1).Value = s.ToString();
            summary.Cell(r, 2).Value = data.ByStatus[s];
        }
        summary.Columns().AdjustToContents();

        var sheet = workbook.Worksheets.Add("Freelancers");
        string[] headers = { "Freelancer", "Completed", "Earned", "Avg rating" };
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).Value = headers[c];
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var f in data.TopFreelancers)
        {
            sheet.Cell(row, 1).Value = f.Name;
            sheet.Cell(row, 2).Value = f.Completed;
            sheet.Cell(row, 3).Value = f.Earned;
            if (f.AvgRating is { } rating) sheet.Cell(row, 4).Value = rating;
            else sheet.Cell(row, 4).Value = "—";
            row++;
        }
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        _logger.LogInformation("Marketplace report XLSX generated. By: {UserId}", userId);
        return Result<byte[]>.Ok(stream.ToArray());
    }
}
