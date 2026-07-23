using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Notifications;
using Taskpilot.API.Hubs;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests the quiet-hours window itself (wrap-around, time zones) and that
/// <see cref="NotificationDeliveryService"/> actually holds back out-of-band channels.
/// </summary>
public class QuietHoursTests
{
    // 2026-07-14 23:30 UTC — late evening in UTC, and 02:30 next day in Kyiv (UTC+3).
    private static readonly DateTime LateEveningUtc = new(2026, 7, 14, 23, 30, 0, DateTimeKind.Utc);
    // 2026-07-14 12:00 UTC — the middle of the working day everywhere in Europe.
    private static readonly DateTime MiddayUtc = new(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Window_WrappingMidnight_IsQuietLateAndEarly()
    {
        // 22:00 → 08:00 in UTC.
        Assert.True(QuietHours.IsQuiet(22, 8, null, LateEveningUtc));                 // 23:30 → quiet
        Assert.True(QuietHours.IsQuiet(22, 8, null, MiddayUtc.AddHours(-9)));         // 03:00 → quiet
        Assert.False(QuietHours.IsQuiet(22, 8, null, MiddayUtc));                     // 12:00 → not quiet
        Assert.False(QuietHours.IsQuiet(22, 8, null, MiddayUtc.AddHours(9)));         // 21:00 → not quiet
    }

    [Fact]
    public void Window_WithinOneDay_DoesNotWrap()
    {
        // A daytime window: 09:00 → 17:00.
        Assert.True(QuietHours.IsQuiet(9, 17, null, MiddayUtc));                       // 12:00 → quiet
        Assert.False(QuietHours.IsQuiet(9, 17, null, LateEveningUtc));                 // 23:30 → not quiet
        // The end hour is exclusive.
        Assert.False(QuietHours.IsQuiet(9, 17, null, new DateTime(2026, 7, 14, 17, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Window_EmptyRange_IsNeverQuiet()
    {
        Assert.False(QuietHours.IsQuiet(22, 22, null, LateEveningUtc));
        Assert.False(QuietHours.IsQuiet(22, 22, null, MiddayUtc));
    }

    [Fact]
    public void TimeZone_ShiftsTheWindow()
    {
        // 23:30 UTC is 02:30 in Kyiv (UTC+3 in July) — quiet under a 22→08 window.
        Assert.True(QuietHours.IsQuiet(22, 8, "Europe/Kyiv", LateEveningUtc));

        // The same instant is 16:30 in Los Angeles (UTC-7) — the middle of the afternoon.
        Assert.False(QuietHours.IsQuiet(22, 8, "America/Los_Angeles", LateEveningUtc));
    }

    [Fact]
    public void UnknownTimeZone_FallsBackToUtc_AndIsRejectedByValidation()
    {
        // An unknown id must not accidentally silence notifications: treat it as UTC.
        Assert.True(QuietHours.IsQuiet(22, 8, "Mars/Olympus_Mons", LateEveningUtc));
        // …but the API refuses to store it in the first place.
        Assert.False(QuietHours.IsKnownTimeZone("Mars/Olympus_Mons"));
        Assert.True(QuietHours.IsKnownTimeZone("Europe/Kyiv"));
        Assert.True(QuietHours.IsKnownTimeZone(null)); // null == UTC
    }

    [Fact]
    public void HourValidation()
    {
        Assert.True(QuietHours.IsValidHour(0));
        Assert.True(QuietHours.IsValidHour(23));
        Assert.False(QuietHours.IsValidHour(-1));
        Assert.False(QuietHours.IsValidHour(24));
    }

    /// <summary>Builds a delivery service whose channels are all mocked and observable.</summary>
    private static (NotificationDeliveryService svc, Mock<IEmailSender> email, Mock<IPushService> push)
        CreateDelivery(TaskpilotDbContext ctx)
    {
        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<EmailAttachment?>()))
             .Returns(Task.CompletedTask);

        var telegram = new Mock<ITelegramSender>();
        telegram.SetupGet(t => t.IsEnabled).Returns(false);
        var viber = new Mock<IViberSender>();
        viber.SetupGet(v => v.IsEnabled).Returns(false);

        var push = new Mock<IPushService>();
        push.Setup(p => p.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var options = Options.Create(new EmailOptions { FrontendBaseUrl = "https://app.test" });
        // The email/Telegram/Viber fan-out runs through the shared dispatcher; the recipient
        // snapshot is loaded by the real resolver over the same context.
        var dispatcher = new NotificationDispatcher(email.Object, telegram.Object, viber.Object, options);
        var resolver = new NotificationRecipientResolver(ctx);
        var svc = new NotificationDeliveryService(ctx, resolver, dispatcher, push.Object, options);
        return (svc, email, push);
    }

    /// <summary>Puts the user in (or out of) a quiet window that covers every hour but one.</summary>
    private static async Task SetQuietHoursAsync(TaskpilotDbContext ctx, Guid userId, bool enabled, int start, int end)
    {
        var user = await ctx.Users.FindAsync(userId);
        user!.QuietHoursEnabled = enabled;
        user.QuietHoursStart = start;
        user.QuietHoursEnd = end;
        user.TimeZoneId = null; // UTC keeps the test independent of the machine's zone
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Delivery_InsideQuietHours_SendsNothingOutOfBand()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Sleeper");
        // A window covering the whole day (00:00 → 23:00 plus the wrap) — always quiet now.
        var hourNow = DateTime.UtcNow.Hour;
        await SetQuietHoursAsync(ctx, user, enabled: true, start: hourNow, end: (hourNow + 1) % 24);
        var (svc, email, push) = CreateDelivery(ctx);

        await svc.DeliverAsync(user, NotificationType.Task, "Task overdue", "/projects/1");

        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Never);
        push.Verify(p => p.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Delivery_OutsideQuietHours_StillSends()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Awake");
        // A one-hour window that does NOT contain the current hour.
        var hourNow = DateTime.UtcNow.Hour;
        await SetQuietHoursAsync(ctx, user, enabled: true, start: (hourNow + 2) % 24, end: (hourNow + 3) % 24);
        var (svc, email, push) = CreateDelivery(ctx);

        await svc.DeliverAsync(user, NotificationType.Task, "Task overdue", "/projects/1");

        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Once);
        push.Verify(p => p.SendToUserAsync(user, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Delivery_QuietHoursDisabled_StillSends()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "NoQuiet");
        var hourNow = DateTime.UtcNow.Hour;
        // The window would cover right now — but the feature is off.
        await SetQuietHoursAsync(ctx, user, enabled: false, start: hourNow, end: (hourNow + 1) % 24);
        var (svc, email, _) = CreateDelivery(ctx);

        await svc.DeliverAsync(user, NotificationType.Task, "Task overdue", "/projects/1");

        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Once);
    }

    /// <summary>Builds a NotificationService (hub, delivery and queue all inert).</summary>
    private static NotificationService CreateNotifications(TaskpilotDbContext ctx)
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
             .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<NotificationHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);

        var delivery = new Mock<INotificationDeliveryService>();
        delivery.Setup(d => d.DeliverAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.CompletedTask);

        return new NotificationService(ctx, hub.Object, delivery.Object, new NotificationRecipientResolver(ctx),
            new DisabledNotificationQueue(), NullLogger<NotificationService>.Instance);
    }

    [Fact]
    public async Task SetQuietHours_RejectsBadHoursAndZones()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var svc = CreateNotifications(ctx);

        Assert.False((await svc.SetQuietHoursAsync(user, new QuietHoursDto { Start = 24, End = 8 })).Succeeded);
        Assert.False((await svc.SetQuietHoursAsync(user, new QuietHoursDto { Start = 22, End = -1 })).Succeeded);
        Assert.False((await svc.SetQuietHoursAsync(user, new QuietHoursDto { Start = 22, End = 8, TimeZoneId = "Nope/Nope" })).Succeeded);

        var ok = await svc.SetQuietHoursAsync(user, new QuietHoursDto
        {
            Enabled = true, Start = 22, End = 8, TimeZoneId = "Europe/Kyiv",
        });
        Assert.True(ok.Succeeded);
        Assert.Equal("Europe/Kyiv", ok.Value!.TimeZoneId);

        var read = await svc.GetQuietHoursAsync(user);
        Assert.True(read.Value!.Enabled);
        Assert.Equal(22, read.Value.Start);
    }
}
