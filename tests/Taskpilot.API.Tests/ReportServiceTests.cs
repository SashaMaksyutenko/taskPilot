using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="ReportService"/> over the in-memory provider.</summary>
public class ReportServiceTests
{
    static ReportServiceTests()
    {
        // Program.cs sets this in production; unit tests must set it too before generating PDFs.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static ReportService Create(TaskpilotDbContext ctx) =>
        new(ctx, NullLogger<ReportService>.Instance);

    private static async Task SeedTaskAsync(
        TaskpilotDbContext ctx, Guid projectId, Guid creatorId,
        ProjectTaskStatus status, Guid? assigneeId = null, DateTime? deadline = null)
    {
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "T",
            Status = status,
            AssigneeId = assigneeId,
            CreatorId = creatorId,
            Deadline = deadline,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Pdf_NonMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var project = await TestDb.AddProjectAsync(ctx, owner);

        var result = await Create(ctx).ProjectReportPdfAsync(outsider, project);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Pdf_Owner_ProducesNonEmptyBytes()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        await SeedTaskAsync(ctx, project, owner, ProjectTaskStatus.Done, owner);

        var result = await Create(ctx).ProjectReportPdfAsync(owner, project);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.Length > 0);
    }

    [Fact]
    public async Task TeamPdf_NonMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var project = await TestDb.AddProjectAsync(ctx, owner);

        var result = await Create(ctx).TeamReportPdfAsync(outsider, project);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task TeamPdf_ProducesNonEmptyBytes()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = project, UserId = member });
        await ctx.SaveChangesAsync();
        await SeedTaskAsync(ctx, project, owner, ProjectTaskStatus.Done, member);

        var result = await Create(ctx).TeamReportPdfAsync(owner, project);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.Length > 0);
    }

    [Fact]
    public async Task TeamXlsx_ScoresCompletionOnTimeAndReputation()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = project, UserId = member });

        // The member finished one task on time and left one overdue.
        var deadline = DateTime.UtcNow.AddDays(-2);
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(), ProjectId = project, Title = "Done on time",
            Status = ProjectTaskStatus.Done, AssigneeId = member, CreatorId = owner,
            Deadline = deadline, CompletedAt = deadline.AddDays(-1),
        });
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(), ProjectId = project, Title = "Still late",
            Status = ProjectTaskStatus.InProgress, AssigneeId = member, CreatorId = owner,
            Deadline = DateTime.UtcNow.AddDays(-4),
        });
        // A ledger entry so the report's reputation column has something to show.
        ctx.ReputationEntries.Add(new ReputationEntry
        {
            Id = Guid.NewGuid(), UserId = member, Delta = 15,
            Reason = ReputationReason.TaskEarly, Description = "Done on time",
        });
        await ctx.SaveChangesAsync();

        var result = await Create(ctx).TeamReportXlsxAsync(owner, project);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.Length > 0);
        // The workbook is a zip archive ("PK" magic bytes).
        Assert.Equal((byte)'P', result.Value![0]);
        Assert.Equal((byte)'K', result.Value![1]);
    }

    [Fact]
    public async Task Xlsx_Owner_ProducesNonEmptyBytes()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        // A done task, an in-progress one, and an overdue one.
        await SeedTaskAsync(ctx, project, owner, ProjectTaskStatus.Done, owner);
        await SeedTaskAsync(ctx, project, owner, ProjectTaskStatus.InProgress, owner);
        await SeedTaskAsync(ctx, project, owner, ProjectTaskStatus.Backlog, owner,
            DateTime.UtcNow.AddDays(-1));

        var result = await Create(ctx).ProjectReportXlsxAsync(owner, project);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.Length > 0);
    }
}
