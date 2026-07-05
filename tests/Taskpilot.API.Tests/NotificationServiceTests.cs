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

    private static NotificationService CreateService(TaskpilotDbContext ctx, IEmailSender email)
    {
        var opts = Options.Create(new EmailOptions { FrontendBaseUrl = "http://localhost:5173" });
        return new NotificationService(ctx, MockHub(), email, opts, NullLogger<NotificationService>.Instance);
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
        email.Verify(e => e.SendAsync("dana@example.com", "Dana", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_OptedOut_SendsNothing()
    {
        await using var ctx = CreateContext();
        var userId = Guid.NewGuid();
        ctx.Users.Add(new User { Id = userId, Name = "Dana", Email = "dana@example.com", Role = Role.Developer, IsActive = true });
        // The recipient opted out of Task notifications entirely.
        ctx.NotificationPreferences.Add(new NotificationPreference { Id = Guid.NewGuid(), UserId = userId, Type = NotificationType.Task });
        await ctx.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        email.SetupGet(e => e.IsEnabled).Returns(true);
        var svc = CreateService(ctx, email.Object);

        await svc.CreateAsync(userId, NotificationType.Task, "You were assigned a task.", null);

        // Nothing stored, nothing emailed.
        Assert.Equal(0, await ctx.Notifications.CountAsync());
        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
