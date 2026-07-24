using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.Hubs;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="NotificationService"/>'s email side-channel, using the
/// in-memory EF provider, a mocked SignalR hub and a mocked email sender.
/// </summary>
public class NotificationServiceTests
{
    private static TaskpilotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TaskpilotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TaskpilotDbContext(options);
    }

    /// <summary>Builds a hub context whose SendAsync is a no-op.</summary>
    private static IHubContext<NotificationHub> MockHub()
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
             .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<NotificationHub>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        return hub.Object;
    }

    // Builds NotificationService over the real inline delivery path (RabbitMQ
    // disabled), so these tests cover the default in-app + email + muting behaviour.
    private static NotificationService CreateService(TaskpilotDbContext ctx, IEmailSender email)
    {
        var opts = Options.Create(new EmailOptions { FrontendBaseUrl = "http://localhost:5173" });
        // Telegram, Viber and push disabled in tests.
        var telegram = new Mock<ITelegramSender>();
        telegram.SetupGet(t => t.IsEnabled).Returns(false);
        var viber = new Mock<IViberSender>();
        viber.SetupGet(v => v.IsEnabled).Returns(false);
        var push = new Mock<IPushService>();
        push.SetupGet(p => p.IsEnabled).Returns(false);
        // The email/Telegram/Viber fan-out runs through the shared dispatcher; the recipient
        // snapshot (and quiet-hours check) come from the real resolver over the same context.
        var dispatcher = new NotificationDispatcher(email, telegram.Object, viber.Object, opts);
        var resolver = new NotificationRecipientResolver(ctx);
        return new NotificationService(ctx, MockHub(), resolver, dispatcher, push.Object,
            new DisabledNotificationQueue(), opts, NullLogger<NotificationService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_EmailEnabled_SendsEmailToRecipient()
    {
        await using var ctx = CreateContext();
        var userId = Guid.NewGuid();
        ctx.Users.Add(new User { Id = userId, Name = "Dana", Email = "dana@example.com", Role = Role.Developer, IsActive = true });
        await ctx.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        var svc = CreateService(ctx, email.Object);

        await svc.CreateAsync(userId, NotificationType.Task, "You were assigned a task.", "/projects/1");

        // The in-app notification is stored and an email is sent to the recipient.
        Assert.Equal(1, await ctx.Notifications.CountAsync());
        email.Verify(e => e.SendAsync("dana@example.com", "Dana", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_BothChannelsMuted_SendsNothing()
    {
        await using var ctx = CreateContext();
        var userId = Guid.NewGuid();
        ctx.Users.Add(new User { Id = userId, Name = "Dana", Email = "dana@example.com", Role = Role.Developer, IsActive = true });
        // Opted out of Task notifications on both channels.
        ctx.NotificationPreferences.Add(new NotificationPreference { Id = Guid.NewGuid(), UserId = userId, Type = NotificationType.Task, Channel = NotificationChannel.InApp });
        ctx.NotificationPreferences.Add(new NotificationPreference { Id = Guid.NewGuid(), UserId = userId, Type = NotificationType.Task, Channel = NotificationChannel.Email });
        await ctx.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        var svc = CreateService(ctx, email.Object);

        await svc.CreateAsync(userId, NotificationType.Task, "You were assigned a task.", null);

        Assert.Equal(0, await ctx.Notifications.CountAsync());
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_EmailMutedOnly_StoresInAppButSkipsEmail()
    {
        await using var ctx = CreateContext();
        var userId = Guid.NewGuid();
        ctx.Users.Add(new User { Id = userId, Name = "Dana", Email = "dana@example.com", Role = Role.Developer, IsActive = true });
        // Muted email for Task, but the in-app channel stays on.
        ctx.NotificationPreferences.Add(new NotificationPreference { Id = Guid.NewGuid(), UserId = userId, Type = NotificationType.Task, Channel = NotificationChannel.Email });
        await ctx.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        var svc = CreateService(ctx, email.Object);

        await svc.CreateAsync(userId, NotificationType.Task, "You were assigned a task.", null);

        // In-app notification stored; email skipped.
        Assert.Equal(1, await ctx.Notifications.CountAsync());
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ToAMemberWhoMutedTheProject_SuppressesEverything()
    {
        await using var ctx = CreateContext();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        ctx.Users.Add(new User { Id = userId, Name = "Dana", Email = "dana@example.com", Role = Role.Developer, IsActive = true });
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId, Muted = true });
        await ctx.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        var svc = CreateService(ctx, email.Object);

        await svc.CreateAsync(userId, NotificationType.Task, "You were assigned a task.", $"/projects/{projectId}");

        // A muted project stores no in-app notification and sends no email.
        Assert.Equal(0, await ctx.Notifications.CountAsync());
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ToAMemberWhoDidNotMuteTheProject_StillNotifies()
    {
        await using var ctx = CreateContext();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        ctx.Users.Add(new User { Id = userId, Name = "Dana", Email = "dana@example.com", Role = Role.Developer, IsActive = true });
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId, Muted = false });
        await ctx.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        var svc = CreateService(ctx, email.Object);

        await svc.CreateAsync(userId, NotificationType.Task, "You were assigned a task.", $"/projects/{projectId}");

        // An unmuted project behaves normally: in-app stored and email sent.
        Assert.Equal(1, await ctx.Notifications.CountAsync());
        email.Verify(e => e.SendAsync("dana@example.com", "Dana", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_QueueEnabled_PublishesInsteadOfDeliveringInline()
    {
        await using var ctx = CreateContext();
        var userId = Guid.NewGuid();
        ctx.Users.Add(new User { Id = userId, Name = "Dana", Email = "dana@example.com", Role = Role.Developer, IsActive = true });
        await ctx.SaveChangesAsync();

        var opts = Options.Create(new EmailOptions { FrontendBaseUrl = "http://localhost:5173" });
        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        var dispatcher = new NotificationDispatcher(email.Object, Mock.Of<ITelegramSender>(), Mock.Of<IViberSender>(), opts);
        var push = new Mock<IPushService>();
        var queue = new Mock<INotificationQueue>();
        queue.SetupGet(q => q.IsEnabled).Returns(true);
        var resolver = new NotificationRecipientResolver(ctx);
        var svc = new NotificationService(ctx, MockHub(), resolver, dispatcher, push.Object, queue.Object, opts,
            NullLogger<NotificationService>.Instance);

        await svc.CreateAsync(userId, NotificationType.Task, "You were assigned a task.", "/projects/1");

        // In-app still stored inline; email/Telegram/Viber handed to the queue with the
        // recipient snapshot, NOT dispatched inline. Web push is sent from the publisher.
        Assert.Equal(1, await ctx.Notifications.CountAsync());
        queue.Verify(q => q.PublishAsync(It.Is<NotificationDeliveryMessage>(
            m => m.RecipientId == userId && m.Type == NotificationType.Task
                 && m.Recipient != null && m.Recipient.Email == "dana@example.com")), Times.Once);
        push.Verify(p => p.SendToUserAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        // The email sender is NOT called inline — that happens in the consumer.
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_InsideQuietHours_StoresInAppButSendsNothingOutOfBand()
    {
        await using var ctx = CreateContext();
        var userId = Guid.NewGuid();
        var hourNow = DateTime.UtcNow.Hour;
        ctx.Users.Add(new User
        {
            Id = userId, Name = "Dana", Email = "dana@example.com", Role = Role.Developer, IsActive = true,
            // A one-hour UTC window covering right now.
            QuietHoursEnabled = true, QuietHoursStart = hourNow, QuietHoursEnd = (hourNow + 1) % 24, TimeZoneId = null,
        });
        await ctx.SaveChangesAsync();

        var opts = Options.Create(new EmailOptions { FrontendBaseUrl = "http://localhost:5173" });
        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        var dispatcher = new NotificationDispatcher(email.Object, Mock.Of<ITelegramSender>(), Mock.Of<IViberSender>(), opts);
        var push = new Mock<IPushService>();
        var svc = new NotificationService(ctx, MockHub(), new NotificationRecipientResolver(ctx), dispatcher,
            push.Object, new DisabledNotificationQueue(), opts, NullLogger<NotificationService>.Instance);

        await svc.CreateAsync(userId, NotificationType.Task, "You were assigned a task.", "/projects/1");

        // The in-app bell still fires; every out-of-band channel is held back.
        Assert.Equal(1, await ctx.Notifications.CountAsync());
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Never);
        push.Verify(p => p.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }
}
