using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Reports;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="ReportScheduleService"/> (recurring report emails).</summary>
public class ReportScheduleServiceTests
{
    private static (ReportScheduleService svc, Mock<IEmailSender> email, Mock<IReportService> reports) Create(
        TaskpilotDbContext ctx, bool emailEnabled = true)
    {
        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(emailEnabled);
        email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<EmailAttachment?>()))
             .Returns(Task.CompletedTask);

        var reports = new Mock<IReportService>();
        reports.Setup(r => r.ProjectReportPdfAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
               .ReturnsAsync(Result<byte[]>.Ok(new byte[] { 1, 2, 3 }));
        reports.Setup(r => r.TeamReportXlsxAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
               .ReturnsAsync(Result<byte[]>.Ok(new byte[] { 4, 5, 6 }));

        var svc = new ReportScheduleService(ctx, reports.Object, email.Object,
            NullLogger<ReportScheduleService>.Instance);
        return (svc, email, reports);
    }

    private static CreateReportScheduleDto Dto(string kind = "Project", string format = "Pdf", string freq = "Weekly") =>
        new() { Kind = kind, Format = format, Frequency = freq };

    [Fact]
    public async Task Create_ThenListReturnsIt()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        var (svc, _, _) = Create(ctx);

        var created = await svc.CreateAsync(owner, project, Dto());

        Assert.True(created.Succeeded);
        Assert.Equal("Weekly", created.Value!.Frequency);
        Assert.Single((await svc.GetForProjectAsync(owner, project)).Value!);
    }

    [Fact]
    public async Task Create_NonMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        var (svc, _, _) = Create(ctx);

        Assert.False((await svc.CreateAsync(outsider, project, Dto())).Succeeded);
    }

    [Fact]
    public async Task Create_InvalidValues_Fail()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        var (svc, _, _) = Create(ctx);

        Assert.False((await svc.CreateAsync(owner, project, Dto(kind: "Nope"))).Succeeded);
        Assert.False((await svc.CreateAsync(owner, project, Dto(format: "Docx"))).Succeeded);
        Assert.False((await svc.CreateAsync(owner, project, Dto(freq: "Hourly"))).Succeeded);
    }

    [Fact]
    public async Task Create_Duplicate_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        var (svc, _, _) = Create(ctx);

        Assert.True((await svc.CreateAsync(owner, project, Dto())).Succeeded);
        Assert.False((await svc.CreateAsync(owner, project, Dto())).Succeeded);
    }

    [Fact]
    public async Task SendDue_MailsReportAsAttachment_AndStampsSent()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        var (svc, email, _) = Create(ctx);
        await svc.CreateAsync(owner, project, Dto()); // Project/Pdf/Weekly, never sent

        var sent = await svc.SendDueAsync();

        Assert.Equal(1, sent);
        // The generated PDF must go out as the attachment.
        email.Verify(e => e.SendAsync(
            It.IsAny<string>(), "Owner", It.IsAny<string>(), It.IsAny<string>(),
            It.Is<EmailAttachment?>(a => a != null && a.FileName.EndsWith(".pdf") && a.Content.Length == 3)),
            Times.Once);

        // The cadence is stamped, so a second run does nothing.
        Assert.Equal(0, await svc.SendDueAsync());
        var schedule = await ctx.ReportSchedules.SingleAsync();
        Assert.NotNull(schedule.LastSentAt);
    }

    [Fact]
    public async Task SendDue_RespectsCadence()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        var (svc, email, _) = Create(ctx);
        await svc.CreateAsync(owner, project, Dto(freq: "Weekly"));

        // Pretend it went out yesterday: a weekly report is not due yet.
        var schedule = await ctx.ReportSchedules.SingleAsync();
        schedule.LastSentAt = DateTime.UtcNow.AddDays(-1);
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await svc.SendDueAsync());
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Never);
    }

    [Fact]
    public async Task SendDue_XlsxSchedule_AttachesWorkbook()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        var (svc, email, _) = Create(ctx);
        await svc.CreateAsync(owner, project, Dto(kind: "Team", format: "Xlsx", freq: "Daily"));

        Assert.Equal(1, await svc.SendDueAsync());
        email.Verify(e => e.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.Is<EmailAttachment?>(a => a != null && a.FileName.EndsWith(".xlsx") && a.Content.Length == 3)),
            Times.Once);
    }

    [Fact]
    public async Task SendDue_EmailDisabled_SendsNothing()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        var (svc, _, _) = Create(ctx, emailEnabled: false);
        await svc.CreateAsync(owner, project, Dto());

        Assert.Equal(0, await svc.SendDueAsync());
    }

    [Fact]
    public async Task Delete_OnlyOwner()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var project = await TestDb.AddProjectAsync(ctx, owner);
        var (svc, _, _) = Create(ctx);
        var created = (await svc.CreateAsync(owner, project, Dto())).Value!;

        Assert.False((await svc.DeleteAsync(other, created.Id)).Succeeded);
        Assert.True((await svc.DeleteAsync(owner, created.Id)).Succeeded);
        Assert.Equal(0, await ctx.ReportSchedules.CountAsync());
    }
}
