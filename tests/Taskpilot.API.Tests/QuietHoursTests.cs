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
/// Tests the quiet-hours window itself (wrap-around, time zones) and the settings CRUD.
/// That quiet hours actually hold back delivery is covered where it now lives:
/// <see cref="NotificationRecipientResolverTests"/> (resolver returns null) and
/// <see cref="NotificationServiceTests"/> (nothing is sent).
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

    /// <summary>Builds a NotificationService with inert out-of-band channels (this file only
    /// exercises its quiet-hours settings CRUD).</summary>
    private static NotificationService CreateNotifications(TaskpilotDbContext ctx)
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
             .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<NotificationHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);

        var opts = Options.Create(new EmailOptions());
        var dispatcher = new NotificationDispatcher(Mock.Of<IEmailSender>(), Mock.Of<ITelegramSender>(), Mock.Of<IViberSender>(), opts);
        return new NotificationService(ctx, hub.Object, new NotificationRecipientResolver(ctx),
            dispatcher, Mock.Of<IPushService>(), new DisabledNotificationQueue(), opts,
            NullLogger<NotificationService>.Instance);
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
