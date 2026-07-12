using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="DigestService"/> over the in-memory provider.</summary>
public class DigestServiceTests
{
    private static (DigestService svc, Mock<IEmailSender> email) Create(TaskpilotDbContext ctx)
    {
        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
             .Returns(Task.CompletedTask);
        return (new DigestService(ctx, email.Object, NullLogger<DigestService>.Instance), email);
    }

    private static async Task SetDigestAsync(TaskpilotDbContext ctx, Guid userId, DigestFrequency freq, DateTime? lastSent = null)
    {
        var user = await ctx.Users.FirstAsync(u => u.Id == userId);
        user.DigestFrequency = freq;
        user.LastDigestSentAt = lastSent;
        await ctx.SaveChangesAsync();
    }

    private static async Task AddTaskAsync(TaskpilotDbContext ctx, Guid projectId, Guid assigneeId, DateTime? deadline)
    {
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "T",
            Status = ProjectTaskStatus.InProgress,
            AssigneeId = assigneeId,
            CreatorId = assigneeId,
            Deadline = deadline,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task SendsToOptedInUserWithTasks()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dana");
        var project = await TestDb.AddProjectAsync(ctx, user);
        await AddTaskAsync(ctx, project, user, DateTime.UtcNow.AddDays(-1)); // overdue
        await SetDigestAsync(ctx, user, DigestFrequency.Daily);
        var (svc, email) = Create(ctx);

        var sent = await svc.SendDueDigestsAsync();

        Assert.Equal(1, sent);
        email.Verify(e => e.SendAsync(It.IsAny<string>(), "Dana", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        // Cadence is stamped so a second run does nothing.
        Assert.Equal(0, await svc.SendDueDigestsAsync());
    }

    [Fact]
    public async Task SkipsUserWithNoTasks_ButStampsSent()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Empty");
        await SetDigestAsync(ctx, user, DigestFrequency.Daily);
        var (svc, email) = Create(ctx);

        var sent = await svc.SendDueDigestsAsync();

        Assert.Equal(0, sent);
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        var stamped = await ctx.Users.Where(u => u.Id == user).Select(u => u.LastDigestSentAt).FirstAsync();
        Assert.NotNull(stamped);
    }

    [Fact]
    public async Task SkipsOffUsers()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Off");
        var project = await TestDb.AddProjectAsync(ctx, user);
        await AddTaskAsync(ctx, project, user, DateTime.UtcNow.AddDays(-1));
        // DigestFrequency stays Off (default).
        var (svc, email) = Create(ctx);

        Assert.Equal(0, await svc.SendDueDigestsAsync());
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RespectsCadence_NotYetDue()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Recent");
        var project = await TestDb.AddProjectAsync(ctx, user);
        await AddTaskAsync(ctx, project, user, DateTime.UtcNow.AddDays(2));
        // Sent an hour ago: a daily digest is not due yet.
        await SetDigestAsync(ctx, user, DigestFrequency.Daily, DateTime.UtcNow.AddHours(-1));
        var (svc, email) = Create(ctx);

        Assert.Equal(0, await svc.SendDueDigestsAsync());
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task NoEmailProvider_SendsNothing()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dana");
        var project = await TestDb.AddProjectAsync(ctx, user);
        await AddTaskAsync(ctx, project, user, DateTime.UtcNow.AddDays(-1));
        await SetDigestAsync(ctx, user, DigestFrequency.Daily);

        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(false);
        var svc = new DigestService(ctx, email.Object, NullLogger<DigestService>.Instance);

        Assert.Equal(0, await svc.SendDueDigestsAsync());
    }
}
