using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for the marketplace Stripe payment flow, using the in-memory EF
/// provider and a mocked <see cref="IPaymentClient"/> so no network calls happen.
/// </summary>
public class MarketplacePaymentTests
{
    private static MarketplaceService CreateService(TaskpilotDbContext ctx, IPaymentClient payments)
    {
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        var webhooks = new Mock<IWebhookService>();
        webhooks
            .Setup(w => w.DispatchAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
        var audit = new Mock<IAuditService>();
        audit
            .Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        return new MarketplaceService(ctx, notifications.Object, webhooks.Object, payments, audit.Object, new Mock<IReputationService>().Object, NullLogger<MarketplaceService>.Instance);
    }

    private static async Task<(Guid posterId, Guid assigneeId, Guid taskId)> SeedCompletedTaskAsync(TaskpilotDbContext ctx)
    {
        var posterId = await TestDb.AddUserAsync(ctx, "Poster");
        var assigneeId = await TestDb.AddUserAsync(ctx, "Assignee");
        var taskId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = taskId,
            Title = "Build a widget",
            Description = "…",
            Budget = 100m,
            Status = MarketplaceTaskStatus.Completed,
            PosterId = posterId,
            AssigneeId = assigneeId,
        });
        await ctx.SaveChangesAsync();
        return (posterId, assigneeId, taskId);
    }

    [Fact]
    public async Task CreatePayment_WhenPaymentsDisabled_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var (posterId, _, taskId) = await SeedCompletedTaskAsync(ctx);

        var client = new Mock<IPaymentClient>();
        client.SetupGet(c => c.IsEnabled).Returns(false);
        var service = CreateService(ctx, client.Object);

        var result = await service.CreatePaymentAsync(posterId, taskId, "http://ok", "http://cancel");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreatePayment_WhenEnabled_ReturnsUrlAndMarksPending()
    {
        await using var ctx = TestDb.CreateContext();
        var (posterId, _, taskId) = await SeedCompletedTaskAsync(ctx);

        var client = new Mock<IPaymentClient>();
        client.SetupGet(c => c.IsEnabled).Returns(true);
        client
            .Setup(c => c.CreateCheckoutSessionAsync(100m, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result<CheckoutSession>.Ok(new CheckoutSession("cs_test_123", "https://stripe.test/pay")));
        var service = CreateService(ctx, client.Object);

        var result = await service.CreatePaymentAsync(posterId, taskId, "http://ok", "http://cancel");

        Assert.True(result.Succeeded);
        Assert.Equal("https://stripe.test/pay", result.Value);
        var task = await ctx.MarketplaceTasks.FindAsync(taskId);
        Assert.Equal(PaymentStatus.Pending, task!.PaymentStatus);
        Assert.Equal("cs_test_123", task.PaymentSessionId);
    }

    [Fact]
    public async Task CreatePayment_ByNonPoster_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, assigneeId, taskId) = await SeedCompletedTaskAsync(ctx);

        var client = new Mock<IPaymentClient>();
        client.SetupGet(c => c.IsEnabled).Returns(true);
        var service = CreateService(ctx, client.Object);

        var result = await service.CreatePaymentAsync(assigneeId, taskId, "http://ok", "http://cancel");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ConfirmPayment_WhenSessionPaid_MarksPaid()
    {
        await using var ctx = TestDb.CreateContext();
        var (posterId, _, taskId) = await SeedCompletedTaskAsync(ctx);
        var task = await ctx.MarketplaceTasks.FindAsync(taskId);
        task!.PaymentSessionId = "cs_test_123";
        task.PaymentStatus = PaymentStatus.Pending;
        await ctx.SaveChangesAsync();

        var client = new Mock<IPaymentClient>();
        client.Setup(c => c.IsSessionPaidAsync("cs_test_123")).ReturnsAsync(Result<bool>.Ok(true));
        var service = CreateService(ctx, client.Object);

        var result = await service.ConfirmPaymentAsync(posterId, taskId);

        Assert.True(result.Succeeded);
        var updated = await ctx.MarketplaceTasks.FindAsync(taskId);
        Assert.Equal(PaymentStatus.Paid, updated!.PaymentStatus);
        Assert.NotNull(updated.PaidAt);
    }

    [Fact]
    public async Task ConfirmPayment_WhenSessionUnpaid_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var (posterId, _, taskId) = await SeedCompletedTaskAsync(ctx);
        var task = await ctx.MarketplaceTasks.FindAsync(taskId);
        task!.PaymentSessionId = "cs_test_123";
        task.PaymentStatus = PaymentStatus.Pending;
        await ctx.SaveChangesAsync();

        var client = new Mock<IPaymentClient>();
        client.Setup(c => c.IsSessionPaidAsync("cs_test_123")).ReturnsAsync(Result<bool>.Ok(false));
        var service = CreateService(ctx, client.Object);

        var result = await service.ConfirmPaymentAsync(posterId, taskId);

        Assert.False(result.Succeeded);
        var updated = await ctx.MarketplaceTasks.FindAsync(taskId);
        Assert.Equal(PaymentStatus.Pending, updated!.PaymentStatus);
    }

    [Fact]
    public async Task ConfirmPaymentBySession_MarksPaid_AndIsIdempotent()
    {
        await using var ctx = TestDb.CreateContext();
        var (_, _, taskId) = await SeedCompletedTaskAsync(ctx);
        var task = await ctx.MarketplaceTasks.FindAsync(taskId);
        task!.PaymentSessionId = "cs_test_webhook";
        task.PaymentStatus = PaymentStatus.Pending;
        await ctx.SaveChangesAsync();

        var client = new Mock<IPaymentClient>();
        var service = CreateService(ctx, client.Object);

        var first = await service.ConfirmPaymentBySessionAsync("cs_test_webhook");
        var second = await service.ConfirmPaymentBySessionAsync("cs_test_webhook"); // duplicate delivery

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded); // idempotent
        var updated = await ctx.MarketplaceTasks.FindAsync(taskId);
        Assert.Equal(PaymentStatus.Paid, updated!.PaymentStatus);
        Assert.NotNull(updated.PaidAt);
    }

    [Fact]
    public async Task ConfirmPaymentBySession_WithUnknownSession_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var service = CreateService(ctx, new Mock<IPaymentClient>().Object);

        var result = await service.ConfirmPaymentBySessionAsync("cs_test_nope");

        Assert.False(result.Succeeded);
    }
}
