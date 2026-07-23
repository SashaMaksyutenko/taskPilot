using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="NotificationDispatcher"/> — the database-free email/Telegram/Viber
/// fan-out shared by the API's inline path and the notification service. Every channel is
/// mocked so the routing rules (mute, presence, enabled) are observable directly.
/// </summary>
public class NotificationDispatcherTests
{
    private readonly Mock<IEmailSender> _email = new();
    private readonly Mock<ITelegramSender> _telegram = new();
    private readonly Mock<IViberSender> _viber = new();

    private NotificationDispatcher Create()
    {
        // All channels enabled by default; individual tests turn one off to prove the guard.
        _email.SetupGet(e => e.IsEnabled).Returns(true);
        _telegram.SetupGet(t => t.IsEnabled).Returns(true);
        _viber.SetupGet(v => v.IsEnabled).Returns(true);
        var options = Options.Create(new EmailOptions { FrontendBaseUrl = "https://app.test" });
        return new NotificationDispatcher(_email.Object, _telegram.Object, _viber.Object, options);
    }

    private static NotificationRecipient Recipient(
        string? email = "user@test.local", string? name = "User",
        string? telegram = "tg-1", string? viber = "vb-1", bool emailMuted = false) =>
        new(email, name, telegram, viber, emailMuted);

    [Fact]
    public async Task Dispatch_SendsOverEveryLinkedChannel()
    {
        var d = Create();

        await d.DispatchAsync(Recipient(), "Hello", "/projects/1");

        _email.Verify(e => e.SendAsync("user@test.local", "User", It.IsAny<string>(),
            It.Is<string>(h => h.Contains("Hello")), It.IsAny<EmailAttachment?>()), Times.Once);
        // The link is made absolute against the frontend base URL.
        _telegram.Verify(t => t.SendMessageAsync("tg-1", It.Is<string>(s => s.Contains("https://app.test/projects/1"))), Times.Once);
        _viber.Verify(v => v.SendMessageAsync("vb-1", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Dispatch_WhenEmailMuted_SkipsEmailButKeepsOtherChannels()
    {
        var d = Create();

        await d.DispatchAsync(Recipient(emailMuted: true), "Hi", null);

        _email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Never);
        _telegram.Verify(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Dispatch_SkipsAChannelWhenTheRecipientHasNoAddressForIt()
    {
        var d = Create();

        await d.DispatchAsync(Recipient(email: null, telegram: null, viber: "vb-1"), "Hi", null);

        _email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Never);
        _telegram.Verify(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _viber.Verify(v => v.SendMessageAsync("vb-1", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Dispatch_SkipsAChannelWhoseProviderIsDisabled()
    {
        var d = Create();
        // Telegram configured off, even though the recipient has a chat id.
        _telegram.SetupGet(t => t.IsEnabled).Returns(false);

        await d.DispatchAsync(Recipient(), "Hi", null);

        _telegram.Verify(t => t.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<EmailAttachment?>()), Times.Once);
    }
}
