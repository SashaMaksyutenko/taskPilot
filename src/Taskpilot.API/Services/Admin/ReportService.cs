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

    /// <summary>A participant's row in the team-performance report.</summary>
    /// <param name="OnTimePct">Null when the member has finished no task that had a deadline.</param>
    private sealed record TeamRow(
        string Name,
        int Assigned,
        int Done,
        int CompletionPct,
        int? OnTimePct,
        int Overdue,
        int Reputation);

    /// <summary>Everything the team-performance report needs.</summary>
    private sealed record TeamReportData(string ProjectName, List<TeamRow> Rows);

    /// <summary>
    /// Loads the project's participants (owner + members) and scores each on their tasks,
    /// plus their reputation from the persisted ledger. Null when the caller has no access.
    /// </summary>
    private async Task<TeamReportData?> GatherTeamAsync(Guid userId, Guid projectId)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId && (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)))
            .Select(p => new
            {
                p.Name,
                p.OwnerId,
                MemberIds = p.Members.Select(m => m.UserId).ToList(),
            })
            .FirstOrDefaultAsync();
        if (project is null)
            return null;

        // Everyone on the project, owner included, even if they hold no tasks.
        var participantIds = project.MemberIds.Append(project.OwnerId).Distinct().ToList();

        var names = await _context.Users
            .Where(u => participantIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var tasks = await _context.ProjectTasks
            .Where(t => t.ProjectId == projectId && t.AssigneeId != null && participantIds.Contains(t.AssigneeId!.Value))
            .Select(t => new { AssigneeId = t.AssigneeId!.Value, t.Status, t.Deadline, t.CompletedAt })
            .AsNoTracking()
            .ToListAsync();

        // Reputation comes straight from the ledger (sum of every recorded delta).
        var reputation = await _context.ReputationEntries
            .Where(e => participantIds.Contains(e.UserId))
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(e => e.Delta) })
            .ToDictionaryAsync(x => x.UserId, x => x.Total);

        var now = DateTime.UtcNow;
        var rows = new List<TeamRow>();

        foreach (var id in participantIds)
        {
            var mine = tasks.Where(t => t.AssigneeId == id).ToList();
            var assigned = mine.Count;
            var done = mine.Count(t => t.Status == ProjectTaskStatus.Done);
            var overdue = mine.Count(t => t.Status != ProjectTaskStatus.Done && t.Deadline != null && t.Deadline < now);

            // On-time rate is only meaningful over finished tasks that had a deadline.
            var judged = mine.Where(t => t.Status == ProjectTaskStatus.Done && t.Deadline != null && t.CompletedAt != null).ToList();
            var onTime = judged.Count(t => t.CompletedAt <= t.Deadline);

            rows.Add(new TeamRow(
                names.TryGetValue(id, out var name) ? name : "Unknown",
                assigned,
                done,
                assigned > 0 ? (int)Math.Round(done * 100.0 / assigned) : 0,
                judged.Count > 0 ? (int)Math.Round(onTime * 100.0 / judged.Count) : null,
                overdue,
                reputation.TryGetValue(id, out var rep) ? rep : 0));
        }

        // Strongest contributors first.
        var ordered = rows.OrderByDescending(r => r.Done).ThenByDescending(r => r.Assigned).ToList();
        return new TeamReportData(project.Name, ordered);
    }

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

    // The audit log can be huge; a report caps at the most recent slice.
    private const int AuditReportLimit = 500;

    /// <summary>One audit entry in the report.</summary>
    private sealed record AuditRow(DateTime At, string Actor, string Action, string Entity, string Details, string Ip);

    /// <summary>Loads the most recent audit entries. Null when the caller is not an admin.</summary>
    private async Task<List<AuditRow>?> GatherAuditAsync(Guid userId)
    {
        if (!await IsAdminAsync(userId))
            return null;

        var entries = await _context.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(AuditReportLimit)
            .Select(a => new
            {
                a.CreatedAt,
                a.ActorEmail,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Details,
                a.IpAddress,
            })
            .AsNoTracking()
            .ToListAsync();

        return entries.Select(a => new AuditRow(
            a.CreatedAt,
            a.ActorEmail ?? "system",
            a.Action,
            // "User/3f2a…" reads better in a table than two separate columns.
            a.EntityType is null ? "—" : $"{a.EntityType}/{Shorten(a.EntityId)}",
            a.Details ?? string.Empty,
            a.IpAddress ?? "—")).ToList();
    }

    /// <summary>True when the user holds the Admin role.</summary>
    private Task<bool> IsAdminAsync(Guid userId) =>
        _context.Users.AnyAsync(u => u.Id == userId && u.Role == Role.Admin);

    /// <summary>Trims a long id to its first segment so tables stay readable.</summary>
    private static string Shorten(string? id) =>
        string.IsNullOrEmpty(id) ? "—" : (id.Length > 8 ? id[..8] : id);

    /// <inheritdoc />
    public async Task<Result<byte[]>> AuditReportPdfAsync(Guid userId)
    {
        var rows = await GatherAuditAsync(userId);
        if (rows is null)
            return Result<byte[]>.Fail("Only admins can run the audit report.");

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                // Landscape: the audit table is wide.
                page.Size(PageSizes.A4.Landscape());
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Text("Audit log").FontSize(16).Bold();
                    col.Item().Text($"Most recent {rows.Count} entries · generated {DateTime.UtcNow:u}")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);  // when
                        c.RelativeColumn(2);  // actor
                        c.RelativeColumn(2);  // action
                        c.RelativeColumn(2);  // entity
                        c.RelativeColumn(3);  // details
                        c.RelativeColumn(1);  // ip
                    });

                    table.Header(h =>
                    {
                        foreach (var head in new[] { "When (UTC)", "Actor", "Action", "Entity", "Details", "IP" })
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(head).Bold();
                    });

                    foreach (var r in rows)
                    {
                        table.Cell().Padding(3).Text($"{r.At:yyyy-MM-dd HH:mm}");
                        table.Cell().Padding(3).Text(r.Actor);
                        table.Cell().Padding(3).Text(r.Action);
                        table.Cell().Padding(3).Text(r.Entity);
                        table.Cell().Padding(3).Text(r.Details);
                        table.Cell().Padding(3).Text(r.Ip);
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();

        _logger.LogInformation("Audit report PDF generated. By: {UserId}", userId);
        return Result<byte[]>.Ok(pdf);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> AuditReportXlsxAsync(Guid userId)
    {
        var rows = await GatherAuditAsync(userId);
        if (rows is null)
            return Result<byte[]>.Fail("Only admins can run the audit report.");

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Audit");

        string[] headers = { "When (UTC)", "Actor", "Action", "Entity", "Details", "IP" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.At.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(row, 2).Value = r.Actor;
            ws.Cell(row, 3).Value = r.Action;
            ws.Cell(row, 4).Value = r.Entity;
            ws.Cell(row, 5).Value = r.Details;
            ws.Cell(row, 6).Value = r.Ip;
            row++;
        }
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        _logger.LogInformation("Audit report XLSX generated. By: {UserId}", userId);
        return Result<byte[]>.Ok(stream.ToArray());
    }

    /// <summary>Everything the user-activity report shows, as label/value pairs plus a header.</summary>
    private sealed record ActivityData(string Name, string Email, DateTime MemberSince, List<(string Label, int Value)> Metrics);

    /// <summary>
    /// Counts a user's activity across the app. Null when the caller may not see it
    /// (only the user themselves, or an admin).
    /// </summary>
    private async Task<ActivityData?> GatherActivityAsync(Guid callerId, Guid targetUserId)
    {
        if (callerId != targetUserId && !await IsAdminAsync(callerId))
            return null;

        var user = await _context.Users
            .Where(u => u.Id == targetUserId)
            .Select(u => new { u.Name, u.Email, u.CreatedAt })
            .FirstOrDefaultAsync();
        if (user is null)
            return null;

        var metrics = new List<(string, int)>
        {
            ("Successful logins", await _context.AuditLogs
                .CountAsync(a => a.ActorId == targetUserId && a.Action == "auth.login.success")),
            ("Tasks assigned", await _context.ProjectTasks
                .CountAsync(t => t.AssigneeId == targetUserId)),
            ("Tasks completed", await _context.ProjectTasks
                .CountAsync(t => t.AssigneeId == targetUserId && t.Status == ProjectTaskStatus.Done)),
            ("Task comments", await _context.TaskComments
                .CountAsync(c => c.AuthorId == targetUserId)),
            ("Chat messages", await _context.Messages
                .CountAsync(m => m.SenderId == targetUserId)),
            ("Forum topics", await _context.ForumTopics
                .CountAsync(t => t.AuthorId == targetUserId)),
            ("Forum replies", await _context.ForumReplies
                .CountAsync(r => r.AuthorId == targetUserId)),
            ("Accepted solutions", await _context.ForumReplies
                .CountAsync(r => r.AuthorId == targetUserId && r.IsSolution)),
            ("Marketplace tasks posted", await _context.MarketplaceTasks
                .CountAsync(t => t.PosterId == targetUserId)),
            ("Marketplace applications", await _context.TaskApplications
                .CountAsync(a => a.ApplicantId == targetUserId)),
            ("Marketplace tasks delivered", await _context.MarketplaceTasks
                .CountAsync(t => t.AssigneeId == targetUserId && t.Status == MarketplaceTaskStatus.Completed)),
            // Straight from the persisted ledger, so it matches the profile's history.
            ("Reputation (ledger)", await _context.ReputationEntries
                .Where(e => e.UserId == targetUserId)
                .SumAsync(e => (int?)e.Delta) ?? 0),
        };

        return new ActivityData(user.Name, user.Email, user.CreatedAt, metrics);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> UserActivityReportPdfAsync(Guid callerId, Guid targetUserId)
    {
        var data = await GatherActivityAsync(callerId, targetUserId);
        if (data is null)
            return Result<byte[]>.Fail("You can only run this report for yourself.");

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text($"Activity report — {data.Name}").FontSize(18).Bold();
                    col.Item().Text($"{data.Email} · member since {data.MemberSince:yyyy-MM-dd} · generated {DateTime.UtcNow:u}")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(); });
                    table.Header(h =>
                    {
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Metric").Bold();
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Value").Bold();
                    });
                    foreach (var (label, value) in data.Metrics)
                    {
                        table.Cell().Padding(4).Text(label);
                        table.Cell().Padding(4).Text(value.ToString());
                    }
                });
            });
        }).GeneratePdf();

        _logger.LogInformation("Activity report PDF generated. TargetUserId: {TargetUserId}, By: {CallerId}",
            targetUserId, callerId);
        return Result<byte[]>.Ok(pdf);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> UserActivityReportXlsxAsync(Guid callerId, Guid targetUserId)
    {
        var data = await GatherActivityAsync(callerId, targetUserId);
        if (data is null)
            return Result<byte[]>.Fail("You can only run this report for yourself.");

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Activity");

        ws.Cell(1, 1).Value = "User";
        ws.Cell(1, 2).Value = data.Name;
        ws.Cell(2, 1).Value = "Email";
        ws.Cell(2, 2).Value = data.Email;
        ws.Cell(3, 1).Value = "Member since";
        ws.Cell(3, 2).Value = data.MemberSince.ToString("yyyy-MM-dd");
        ws.Cell(4, 1).Value = "Generated (UTC)";
        ws.Cell(4, 2).Value = DateTime.UtcNow.ToString("u");

        const int headerRow = 6;
        ws.Cell(headerRow, 1).Value = "Metric";
        ws.Cell(headerRow, 2).Value = "Value";
        ws.Row(headerRow).Style.Font.Bold = true;

        var row = headerRow + 1;
        foreach (var (label, value) in data.Metrics)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            row++;
        }
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        _logger.LogInformation("Activity report XLSX generated. TargetUserId: {TargetUserId}, By: {CallerId}",
            targetUserId, callerId);
        return Result<byte[]>.Ok(stream.ToArray());
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> TeamReportPdfAsync(Guid userId, Guid projectId)
    {
        var data = await GatherTeamAsync(userId, projectId);
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
                    col.Item().Text($"Team performance — {data.ProjectName}").FontSize(18).Bold();
                    col.Item().Text($"Generated {DateTime.UtcNow:u}").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3); // member
                        c.RelativeColumn();  // assigned
                        c.RelativeColumn();  // done
                        c.RelativeColumn();  // completion %
                        c.RelativeColumn();  // on-time %
                        c.RelativeColumn();  // overdue
                        c.RelativeColumn();  // reputation
                    });

                    table.Header(h =>
                    {
                        foreach (var head in new[] { "Member", "Assigned", "Done", "Completion", "On time", "Overdue", "Reputation" })
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(head).Bold();
                    });

                    foreach (var r in data.Rows)
                    {
                        table.Cell().Padding(4).Text(r.Name);
                        table.Cell().Padding(4).Text(r.Assigned.ToString());
                        table.Cell().Padding(4).Text(r.Done.ToString());
                        table.Cell().Padding(4).Text($"{r.CompletionPct}%");
                        // An em dash when the member has finished nothing with a deadline.
                        table.Cell().Padding(4).Text(r.OnTimePct is { } pct ? $"{pct}%" : "—");
                        table.Cell().Padding(4).Text(r.Overdue.ToString());
                        table.Cell().Padding(4).Text(r.Reputation.ToString());
                    }
                });
            });
        }).GeneratePdf();

        _logger.LogInformation("Team report PDF generated. ProjectId: {ProjectId}, By: {UserId}", projectId, userId);
        return Result<byte[]>.Ok(pdf);
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> TeamReportXlsxAsync(Guid userId, Guid projectId)
    {
        var data = await GatherTeamAsync(userId, projectId);
        if (data is null)
            return Result<byte[]>.Fail("Project not found.");

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Team");

        ws.Cell(1, 1).Value = "Project";
        ws.Cell(1, 2).Value = data.ProjectName;
        ws.Cell(2, 1).Value = "Generated (UTC)";
        ws.Cell(2, 2).Value = DateTime.UtcNow.ToString("u");

        string[] headers = { "Member", "Assigned", "Done", "Completion %", "On time %", "Overdue", "Reputation" };
        const int headerRow = 4;
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];
        ws.Row(headerRow).Style.Font.Bold = true;

        var row = headerRow + 1;
        foreach (var r in data.Rows)
        {
            ws.Cell(row, 1).Value = r.Name;
            ws.Cell(row, 2).Value = r.Assigned;
            ws.Cell(row, 3).Value = r.Done;
            ws.Cell(row, 4).Value = r.CompletionPct;
            if (r.OnTimePct is { } pct) ws.Cell(row, 5).Value = pct;
            else ws.Cell(row, 5).Value = "—";
            ws.Cell(row, 6).Value = r.Overdue;
            ws.Cell(row, 7).Value = r.Reputation;
            row++;
        }
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        _logger.LogInformation("Team report XLSX generated. ProjectId: {ProjectId}, By: {UserId}", projectId, userId);
        return Result<byte[]>.Ok(stream.ToArray());
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
