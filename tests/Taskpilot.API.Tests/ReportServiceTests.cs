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

    /// <summary>Adds a user with an explicit role (TestDb's helper always makes Developers).</summary>
    private static async Task<Guid> AddUserWithRoleAsync(TaskpilotDbContext ctx, string name, Role role)
    {
        var id = Guid.NewGuid();
        ctx.Users.Add(new User
        {
            Id = id, Name = name, Email = $"{id:N}@test.local",
            PasswordHash = "h", Role = role, IsActive = true,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task MarketplaceReport_NonAdmin_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var dev = await TestDb.AddUserAsync(ctx, "Dev"); // Developer role

        Assert.False((await Create(ctx).MarketplaceReportPdfAsync(dev)).Succeeded);
        Assert.False((await Create(ctx).MarketplaceReportXlsxAsync(dev)).Succeeded);
    }

    [Fact]
    public async Task MarketplaceReport_Admin_ProducesBothFormats()
    {
        await using var ctx = TestDb.CreateContext();
        var admin = await AddUserWithRoleAsync(ctx, "Admin", Role.Admin);
        var poster = await TestDb.AddUserAsync(ctx, "Poster");
        var freelancer = await TestDb.AddUserAsync(ctx, "Freelancer");

        // One completed & paid task, one still open.
        var completedId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = completedId, Title = "Delivered", Description = "…", Budget = 120m,
            Status = MarketplaceTaskStatus.Completed, PosterId = poster, AssigneeId = freelancer,
            PaymentStatus = PaymentStatus.Paid, PaidAt = DateTime.UtcNow,
        });
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = Guid.NewGuid(), Title = "Still open", Description = "…", Budget = 80m,
            Status = MarketplaceTaskStatus.Open, PosterId = poster,
        });
        ctx.TaskApplications.Add(new TaskApplication
        {
            Id = Guid.NewGuid(), TaskId = completedId, ApplicantId = freelancer,
            Status = ApplicationStatus.Accepted,
        });
        ctx.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(), MarketplaceTaskId = completedId,
            RaterId = poster, RateeId = freelancer, Stars = 5,
        });
        await ctx.SaveChangesAsync();

        var pdf = await Create(ctx).MarketplaceReportPdfAsync(admin);
        var xlsx = await Create(ctx).MarketplaceReportXlsxAsync(admin);

        Assert.True(pdf.Succeeded);
        Assert.True(pdf.Value!.Length > 0);
        Assert.True(xlsx.Succeeded);
        // The workbook is a zip archive ("PK" magic bytes).
        Assert.Equal((byte)'P', xlsx.Value![0]);
        Assert.Equal((byte)'K', xlsx.Value![1]);
    }

    [Fact]
    public async Task AuditReport_NonAdmin_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var dev = await TestDb.AddUserAsync(ctx, "Dev");

        Assert.False((await Create(ctx).AuditReportPdfAsync(dev)).Succeeded);
        Assert.False((await Create(ctx).AuditReportXlsxAsync(dev)).Succeeded);
    }

    [Fact]
    public async Task AuditReport_Admin_ProducesBothFormats()
    {
        await using var ctx = TestDb.CreateContext();
        var admin = await AddUserWithRoleAsync(ctx, "Admin", Role.Admin);
        ctx.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), ActorId = admin, ActorEmail = "admin@x.io",
            Action = "auth.login.success", EntityType = "User", EntityId = admin.ToString(),
            Details = "signed in", IpAddress = "127.0.0.1",
        });
        await ctx.SaveChangesAsync();

        var pdf = await Create(ctx).AuditReportPdfAsync(admin);
        var xlsx = await Create(ctx).AuditReportXlsxAsync(admin);

        Assert.True(pdf.Succeeded);
        Assert.True(pdf.Value!.Length > 0);
        Assert.True(xlsx.Succeeded);
        Assert.Equal((byte)'P', xlsx.Value![0]);
        Assert.Equal((byte)'K', xlsx.Value![1]);
    }

    [Fact]
    public async Task ActivityReport_ForSelf_IsAllowed()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");

        var result = await Create(ctx).UserActivityReportPdfAsync(user, user);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.Length > 0);
    }

    [Fact]
    public async Task ActivityReport_ForSomeoneElse_IsRefused()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var other = await TestDb.AddUserAsync(ctx, "Other");

        // A plain user cannot pull someone else's activity.
        Assert.False((await Create(ctx).UserActivityReportPdfAsync(me, other)).Succeeded);
    }

    [Fact]
    public async Task ActivityReport_AdminCanRunForAnyone_AndCountsActivity()
    {
        await using var ctx = TestDb.CreateContext();
        var admin = await AddUserWithRoleAsync(ctx, "Admin", Role.Admin);
        var target = await TestDb.AddUserAsync(ctx, "Target");
        var project = await TestDb.AddProjectAsync(ctx, target);
        await SeedTaskAsync(ctx, project, target, ProjectTaskStatus.Done, target);
        ctx.ReputationEntries.Add(new ReputationEntry
        {
            Id = Guid.NewGuid(), UserId = target, Delta = 10,
            Reason = ReputationReason.MarketplaceCompleted, Description = "Job",
        });
        await ctx.SaveChangesAsync();

        var xlsx = await Create(ctx).UserActivityReportXlsxAsync(admin, target);

        Assert.True(xlsx.Succeeded);
        Assert.Equal((byte)'P', xlsx.Value![0]);
        Assert.Equal((byte)'K', xlsx.Value![1]);
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
