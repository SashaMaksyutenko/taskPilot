using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.ChatBot;
using Taskpilot.API.DTOs.Integrations;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Taskpilot.API.Services.Assistant;
using Taskpilot.Integrations;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests the Telegram update handler: linking commands and routing a linked user's free text to
/// the AI assistant, all replying through a (mocked) Telegram sender.
/// </summary>
public class TelegramUpdateServiceTests
{
    private readonly Mock<ITelegramLinkService> _link = new();
    private readonly Mock<ITelegramSender> _sender = new();
    private readonly Mock<IAssistantAgent> _assistant = new();

    private TelegramUpdateService Make(TaskpilotDbContext ctx)
    {
        _sender.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        return new TelegramUpdateService(ctx, _link.Object, _sender.Object, _assistant.Object,
            NullLogger<TelegramUpdateService>.Instance);
    }

    private static TelegramUpdate Update(long chatId, string? text) =>
        new() { Message = new TelegramMessage { Chat = new TelegramChat { Id = chatId }, Text = text } };

    private static async Task<Guid> AddLinkedUserAsync(TaskpilotDbContext ctx, string chatId, Role role = Role.Developer)
    {
        var id = await TestDb.AddUserAsync(ctx, "Linked");
        var user = (await ctx.Users.FindAsync(id))!;
        user.TelegramChatId = chatId;
        user.Role = role;
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Start_WithCode_LinksAndConfirms()
    {
        await using var ctx = TestDb.CreateContext();
        _link.Setup(l => l.LinkByCodeAsync("ABC123", "555")).ReturnsAsync(true);

        await Make(ctx).HandleUpdateAsync(Update(555, "/start ABC123"));

        _link.Verify(l => l.LinkByCodeAsync("ABC123", "555"), Times.Once);
        _sender.Verify(s => s.SendMessageAsync("555", It.Is<string>(t => t.Contains("linked"))), Times.Once);
    }

    [Fact]
    public async Task BareCode_FromUnlinkedChat_Links()
    {
        await using var ctx = TestDb.CreateContext();
        _link.Setup(l => l.LinkByCodeAsync("54C2EE60", "555")).ReturnsAsync(true);

        // Users link by sending just the code (no "/start"), per the welcome message.
        await Make(ctx).HandleUpdateAsync(Update(555, "54C2EE60"));

        _link.Verify(l => l.LinkByCodeAsync("54C2EE60", "555"), Times.Once);
        _sender.Verify(s => s.SendMessageAsync("555", It.Is<string>(t => t.Contains("linked"))), Times.Once);
        _assistant.Verify(a => a.AskAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ChatBotMessageDto>>()), Times.Never);
    }

    [Fact]
    public async Task Start_WithInvalidCode_ReportsFailure()
    {
        await using var ctx = TestDb.CreateContext();
        _link.Setup(l => l.LinkByCodeAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

        await Make(ctx).HandleUpdateAsync(Update(555, "/start WRONG"));

        _sender.Verify(s => s.SendMessageAsync("555", It.Is<string>(t => t.Contains("invalid") || t.Contains("expired"))), Times.Once);
    }

    [Fact]
    public async Task Start_NoCode_SendsWelcomeWithoutLinking()
    {
        await using var ctx = TestDb.CreateContext();

        await Make(ctx).HandleUpdateAsync(Update(555, "/start"));

        _link.Verify(l => l.LinkByCodeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _sender.Verify(s => s.SendMessageAsync("555", It.Is<string>(t => t.Contains("Welcome"))), Times.Once);
    }

    [Fact]
    public async Task Help_SendsCommandList()
    {
        await using var ctx = TestDb.CreateContext();

        await Make(ctx).HandleUpdateAsync(Update(555, "/help"));

        _sender.Verify(s => s.SendMessageAsync("555", It.Is<string>(t => t.Contains("commands"))), Times.Once);
        _assistant.Verify(a => a.AskAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ChatBotMessageDto>>()), Times.Never);
    }

    [Fact]
    public async Task Unlink_LinkedChat_UnlinksTheUser()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await AddLinkedUserAsync(ctx, "555");
        _link.Setup(l => l.UnlinkAsync(userId)).ReturnsAsync(Result.Ok());

        await Make(ctx).HandleUpdateAsync(Update(555, "/unlink"));

        _link.Verify(l => l.UnlinkAsync(userId), Times.Once);
        _sender.Verify(s => s.SendMessageAsync("555", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task FreeText_FromLinkedUser_AsksTheAssistant_AndRepliesWithTheAnswer()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await AddLinkedUserAsync(ctx, "555");
        _assistant.SetupGet(a => a.IsEnabled).Returns(true);
        _assistant.Setup(a => a.AskAsync(userId, It.IsAny<IReadOnlyList<ChatBotMessageDto>>()))
            .ReturnsAsync(Result<string>.Ok("You have 3 tasks due today."));

        await Make(ctx).HandleUpdateAsync(Update(555, "what is due today?"));

        _assistant.Verify(a => a.AskAsync(userId, It.Is<IReadOnlyList<ChatBotMessageDto>>(
            m => m.Count == 1 && m[0].Content == "what is due today?")), Times.Once);
        _sender.Verify(s => s.SendMessageAsync("555", "You have 3 tasks due today."), Times.Once);
    }

    [Fact]
    public async Task FreeText_FromUnlinkedChat_IsTreatedAsALinkCode_NotAsked()
    {
        await using var ctx = TestDb.CreateContext();
        _link.Setup(l => l.LinkByCodeAsync(It.IsAny<string>(), "555")).ReturnsAsync(false);

        // Not linked yet, so free text is tried as a code (and here it isn't one) — never the assistant.
        await Make(ctx).HandleUpdateAsync(Update(555, "hello there"));

        _link.Verify(l => l.LinkByCodeAsync("there", "555"), Times.Once);
        _assistant.Verify(a => a.AskAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ChatBotMessageDto>>()), Times.Never);
        _sender.Verify(s => s.SendMessageAsync("555", It.Is<string>(t => t.Contains("invalid") || t.Contains("expired"))), Times.Once);
    }

    [Fact]
    public async Task FreeText_FromViewer_IsBlocked()
    {
        await using var ctx = TestDb.CreateContext();
        await AddLinkedUserAsync(ctx, "555", Role.Viewer);
        _assistant.SetupGet(a => a.IsEnabled).Returns(true);

        await Make(ctx).HandleUpdateAsync(Update(555, "create a task"));

        _assistant.Verify(a => a.AskAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ChatBotMessageDto>>()), Times.Never);
        _sender.Verify(s => s.SendMessageAsync("555", It.Is<string>(t => t.Contains("view-only"))), Times.Once);
    }

    [Fact]
    public async Task FreeText_FromBannedUser_IsBlocked_NeverReachesAssistant()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await AddLinkedUserAsync(ctx, "555");
        var user = (await ctx.Users.FindAsync(userId))!;
        user.IsActive = false; // banned or account-closed
        await ctx.SaveChangesAsync();
        _assistant.SetupGet(a => a.IsEnabled).Returns(true);

        await Make(ctx).HandleUpdateAsync(Update(555, "create a task"));

        // The web ban revokes sessions; the Telegram channel must not stay an open back door.
        _assistant.Verify(a => a.AskAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ChatBotMessageDto>>()), Times.Never);
        _sender.Verify(s => s.SendMessageAsync("555", It.Is<string>(t => t.Contains("no longer active"))), Times.Once);
    }

    [Fact]
    public async Task NonTextMessage_IsIgnored()
    {
        await using var ctx = TestDb.CreateContext();

        await Make(ctx).HandleUpdateAsync(Update(555, null));

        _sender.Verify(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
