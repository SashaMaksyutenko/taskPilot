using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Verifies the newly added webhook events fire from the right service methods:
/// <c>user.joined</c> on registration and <c>marketplace.application.accepted</c>
/// when a poster accepts an application.
/// </summary>
public class WebhookEventDispatchTests
{
    [Fact]
    public async Task Register_DispatchesUserJoined()
    {
        await using var ctx = TestDb.CreateContext();
        var webhooks = new Mock<IWebhookService>();
        webhooks.Setup(w => w.DispatchAsync(It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);

        var token = new Mock<ITokenService>();
        var svc = new AuthService(
            ctx, token.Object,
            new Mock<IGoogleAuthClient>().Object,
            new Mock<IGitHubAuthClient>().Object,
            new Mock<ILinkedInAuthClient>().Object,
            webhooks.Object,
            Options.Create(new JwtSettings { RefreshTokenDays = 7 }),
            NullLogger<AuthService>.Instance);

        var result = await svc.RegisterAsync(new RegisterDto { Name = "Ann", Email = "ann@example.com", Password = "Passw0rd!" });

        Assert.True(result.Succeeded);
        webhooks.Verify(w => w.DispatchAsync(WebhookEvents.UserJoined, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task AcceptApplication_DispatchesApplicationAccepted()
    {
        await using var ctx = TestDb.CreateContext();
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        var webhooks = new Mock<IWebhookService>();
        webhooks.Setup(w => w.DispatchAsync(It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var svc = new MarketplaceService(ctx, notifications.Object, webhooks.Object,
            new Mock<IPaymentClient>().Object, audit.Object, NullLogger<MarketplaceService>.Instance);

        var posterId = await TestDb.AddUserAsync(ctx, "Poster");
        var applicantId = await TestDb.AddUserAsync(ctx, "Applicant");
        var taskId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = taskId,
            Title = "Build a widget",
            Description = "…",
            Budget = 100m,
            Status = MarketplaceTaskStatus.Open,
            PosterId = posterId,
        });
        var applicationId = Guid.NewGuid();
        ctx.TaskApplications.Add(new TaskApplication
        {
            Id = applicationId,
            TaskId = taskId,
            ApplicantId = applicantId,
            Status = ApplicationStatus.Pending,
        });
        await ctx.SaveChangesAsync();

        var result = await svc.DecideApplicationAsync(posterId, applicationId, accept: true);

        Assert.True(result.Succeeded);
        webhooks.Verify(w => w.DispatchAsync(WebhookEvents.MarketplaceApplicationAccepted, It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task RejectApplication_DoesNotDispatchAccepted()
    {
        await using var ctx = TestDb.CreateContext();
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        var webhooks = new Mock<IWebhookService>();
        webhooks.Setup(w => w.DispatchAsync(It.IsAny<string>(), It.IsAny<object>())).Returns(Task.CompletedTask);
        var audit = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var svc = new MarketplaceService(ctx, notifications.Object, webhooks.Object,
            new Mock<IPaymentClient>().Object, audit.Object, NullLogger<MarketplaceService>.Instance);

        var posterId = await TestDb.AddUserAsync(ctx, "Poster");
        var applicantId = await TestDb.AddUserAsync(ctx, "Applicant");
        var taskId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = taskId,
            Title = "Build a widget",
            Description = "…",
            Budget = 100m,
            Status = MarketplaceTaskStatus.Open,
            PosterId = posterId,
        });
        var applicationId = Guid.NewGuid();
        ctx.TaskApplications.Add(new TaskApplication
        {
            Id = applicationId,
            TaskId = taskId,
            ApplicantId = applicantId,
            Status = ApplicationStatus.Pending,
        });
        await ctx.SaveChangesAsync();

        await svc.DecideApplicationAsync(posterId, applicationId, accept: false);

        webhooks.Verify(w => w.DispatchAsync(WebhookEvents.MarketplaceApplicationAccepted, It.IsAny<object>()), Times.Never);
    }
}
