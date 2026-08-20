using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for workspace billing: the Free-plan project gate (active only when Stripe is configured),
/// the Stripe webhook → plan transitions, and the failed-payment grace window + admin alert.
/// </summary>
public class BillingServiceTests
{
    private static BillingService Make(TaskpilotDbContext ctx, bool billingEnabled) => MakeWith(ctx, billingEnabled).svc;

    private static (BillingService svc, Mock<INotificationService> notify) MakeWith(TaskpilotDbContext ctx, bool billingEnabled)
    {
        var stripe = new Mock<IStripeBillingClient>();
        stripe.SetupGet(s => s.IsEnabled).Returns(billingEnabled);
        stripe.Setup(s => s.GetSubscriptionAsync(It.IsAny<string>()))
              .ReturnsAsync(Result<(bool, DateTime?)>.Ok((true, DateTime.UtcNow.AddDays(30))));
        var options = Options.Create(new StripeOptions { SecretKey = "sk_test", ProPriceId = "price_m" });
        var notify = new Mock<INotificationService>();
        return (new BillingService(ctx, stripe.Object, options, notify.Object, NullLogger<BillingService>.Instance), notify);
    }

    private static async Task AddProjectsAsync(TaskpilotDbContext ctx, Guid owner, int count)
    {
        for (var i = 0; i < count; i++) await TestDb.AddProjectAsync(ctx, owner, $"P{i}");
    }

    [Fact]
    public async Task CanCreateProject_Unlimited_WhenBillingDisabled()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        await AddProjectsAsync(ctx, owner, 10);

        var svc = Make(ctx, billingEnabled: false);
        Assert.True(await svc.CanCreateProjectAsync());

        var status = await svc.GetStatusAsync();
        Assert.False(status.BillingEnabled);
        Assert.Equal(-1, status.ProjectLimit); // unlimited
    }

    [Fact]
    public async Task CanCreateProject_GatesFreePlan_WhenBillingEnabled()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        await AddProjectsAsync(ctx, owner, BillingService.FreeProjectLimit); // exactly at the cap
        var svc = Make(ctx, billingEnabled: true);

        Assert.False(await svc.CanCreateProjectAsync()); // Free plan is full

        // Upgrade to Pro → unlimited.
        var settings = new OrganizationSettings { Id = OrganizationSettings.SingletonId };
        ctx.OrganizationSettings.Add(settings);
        settings.Plan = "Pro";
        await ctx.SaveChangesAsync();

        Assert.True(await svc.CanCreateProjectAsync());
        Assert.Equal(-1, (await svc.GetStatusAsync()).ProjectLimit);
    }

    [Fact]
    public async Task CanCreateProject_ArchivedProjects_DoNotCountAgainstFreeLimit()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        await AddProjectsAsync(ctx, owner, BillingService.FreeProjectLimit); // at the cap
        var svc = Make(ctx, billingEnabled: true);
        Assert.False(await svc.CanCreateProjectAsync()); // full while all are live

        // Archiving one frees a slot — archived projects are "put away", not active.
        var one = await ctx.Projects.FirstAsync();
        one.ArchivedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        Assert.True(await svc.CanCreateProjectAsync());
        Assert.Equal(BillingService.FreeProjectLimit - 1, (await svc.GetStatusAsync()).ProjectCount);
    }

    [Fact]
    public async Task IsPro_TrueWhenBillingOff_OrPro_FalseOnFree()
    {
        await using var ctx = TestDb.CreateContext();

        // Billing off ⇒ everything unlocked.
        Assert.True(await Make(ctx, billingEnabled: false).IsProAsync());

        // Billing on + Free ⇒ not Pro.
        var svc = Make(ctx, billingEnabled: true);
        Assert.False(await svc.IsProAsync());

        // Upgrade to Pro.
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId, Plan = "Pro" });
        await ctx.SaveChangesAsync();
        Assert.True(await svc.IsProAsync());
    }

    [Fact]
    public async Task Status_ReportsFreeLimit_WhenEnabledAndFree()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        await AddProjectsAsync(ctx, owner, 2);

        var status = await Make(ctx, billingEnabled: true).GetStatusAsync();
        Assert.Equal("Free", status.Plan);
        Assert.Equal(BillingService.FreeProjectLimit, status.ProjectLimit);
        Assert.Equal(2, status.ProjectCount);
    }

    [Fact]
    public async Task Webhook_CheckoutCompleted_UpgradesToPro()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Make(ctx, billingEnabled: true);

        await svc.ProcessWebhookAsync(
            """{"type":"checkout.session.completed","data":{"object":{"mode":"subscription","customer":"cus_1","subscription":"sub_1"}}}""");

        var s = await ctx.OrganizationSettings.FirstAsync();
        Assert.Equal("Pro", s.Plan);
        Assert.Equal("cus_1", s.StripeCustomerId);
        Assert.Equal("sub_1", s.StripeSubscriptionId);
        Assert.NotNull(s.PlanRenewsAt); // filled from GetSubscriptionAsync
    }

    [Fact]
    public async Task Webhook_SubscriptionDeleted_DowngradesToFree()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings
        {
            Id = OrganizationSettings.SingletonId, Plan = "Pro", StripeSubscriptionId = "sub_1", PlanRenewsAt = DateTime.UtcNow.AddDays(5),
        });
        await ctx.SaveChangesAsync();
        var svc = Make(ctx, billingEnabled: true);

        await svc.ProcessWebhookAsync("""{"type":"customer.subscription.deleted","data":{"object":{"id":"sub_1","status":"canceled"}}}""");

        var s = await ctx.OrganizationSettings.FirstAsync();
        Assert.Equal("Free", s.Plan);
        Assert.Null(s.StripeSubscriptionId);
        Assert.Null(s.PlanRenewsAt);
    }

    [Fact]
    public async Task Webhook_SubscriptionUpdated_TogglesByStatus()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Make(ctx, billingEnabled: true);

        await svc.ProcessWebhookAsync(
            """{"type":"customer.subscription.updated","data":{"object":{"id":"sub_1","status":"active","current_period_end":1790000000}}}""");
        Assert.Equal("Pro", (await ctx.OrganizationSettings.FirstAsync()).Plan);

        await svc.ProcessWebhookAsync(
            """{"type":"customer.subscription.updated","data":{"object":{"id":"sub_1","status":"canceled"}}}""");
        Assert.Equal("Free", (await ctx.OrganizationSettings.FirstAsync()).Plan);
    }

    [Fact]
    public async Task Webhook_PastDue_KeepsProDuringGrace()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Make(ctx, billingEnabled: true);

        await svc.ProcessWebhookAsync(
            """{"type":"customer.subscription.updated","data":{"object":{"id":"sub_1","status":"past_due"}}}""");

        var s = await ctx.OrganizationSettings.FirstAsync();
        Assert.Equal("Pro", s.Plan);   // still Pro during the grace window
        Assert.True(s.PlanPastDue);
        Assert.True(await svc.IsProAsync()); // features stay unlocked
        Assert.True((await svc.GetStatusAsync()).PastDue);
    }

    [Fact]
    public async Task Webhook_PaymentFailed_FlagsAndNotifiesAdmins()
    {
        await using var ctx = TestDb.CreateContext();
        var admin = await TestDb.AddUserAsync(ctx, "Admin");
        (await ctx.Users.FindAsync(admin))!.Role = Role.Admin;
        await ctx.SaveChangesAsync();
        var (svc, notify) = MakeWith(ctx, billingEnabled: true);

        await svc.ProcessWebhookAsync("""{"type":"invoice.payment_failed","data":{"object":{"id":"in_1"}}}""");

        Assert.True((await ctx.OrganizationSettings.FirstAsync()).PlanPastDue);
        notify.Verify(n => n.CreateAsync(admin, NotificationType.General, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Webhook_InvoicePaid_ClearsPastDue()
    {
        await using var ctx = TestDb.CreateContext();
        ctx.OrganizationSettings.Add(new OrganizationSettings { Id = OrganizationSettings.SingletonId, Plan = "Pro", PlanPastDue = true });
        await ctx.SaveChangesAsync();
        var svc = Make(ctx, billingEnabled: true);

        await svc.ProcessWebhookAsync("""{"type":"invoice.paid","data":{"object":{"id":"in_1"}}}""");

        Assert.False((await ctx.OrganizationSettings.FirstAsync()).PlanPastDue);
    }
}
