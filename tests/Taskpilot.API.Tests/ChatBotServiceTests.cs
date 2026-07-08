using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.DTOs.ChatBot;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="ChatBotService"/> using a mocked <see cref="IChatBotClient"/>
/// so no OpenAI network calls happen.
/// </summary>
public class ChatBotServiceTests
{
    private static ChatBotMessageDto User(string content) => new() { Role = "user", Content = content };

    [Fact]
    public async Task Ask_PrependsSystemPromptAndReturnsReply()
    {
        var client = new Mock<IChatBotClient>();
        client.SetupGet(c => c.IsEnabled).Returns(true);
        IReadOnlyList<ChatBotMessage>? captured = null;
        client.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<ChatBotMessage>>()))
            .Callback<IReadOnlyList<ChatBotMessage>>(m => captured = m)
            .ReturnsAsync(Result<string>.Ok("Here's how."));
        var service = new ChatBotService(client.Object);

        var result = await service.AskAsync(new[] { User("How do I add a task?") });

        Assert.True(result.Succeeded);
        Assert.Equal("Here's how.", result.Value);
        // A system prompt is prepended before the user's message.
        Assert.NotNull(captured);
        Assert.Equal("system", captured![0].Role);
        Assert.Equal("user", captured[^1].Role);
    }

    [Fact]
    public async Task Ask_WhenEmptyConversation_Fails()
    {
        var client = new Mock<IChatBotClient>();
        client.SetupGet(c => c.IsEnabled).Returns(true);
        var service = new ChatBotService(client.Object);

        var result = await service.AskAsync(Array.Empty<ChatBotMessageDto>());

        Assert.False(result.Succeeded);
        client.Verify(c => c.CompleteAsync(It.IsAny<IReadOnlyList<ChatBotMessage>>()), Times.Never);
    }

    [Fact]
    public async Task Ask_WhenClientDisabled_PropagatesFailure()
    {
        var client = new Mock<IChatBotClient>();
        client.SetupGet(c => c.IsEnabled).Returns(false);
        client.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<ChatBotMessage>>()))
            .ReturnsAsync(Result<string>.Fail("The AI assistant is not configured."));
        var service = new ChatBotService(client.Object);

        var result = await service.AskAsync(new[] { User("Hi") });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Ask_TrimsHistoryToRecentTurns()
    {
        var client = new Mock<IChatBotClient>();
        client.SetupGet(c => c.IsEnabled).Returns(true);
        IReadOnlyList<ChatBotMessage>? captured = null;
        client.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<ChatBotMessage>>()))
            .Callback<IReadOnlyList<ChatBotMessage>>(m => captured = m)
            .ReturnsAsync(Result<string>.Ok("ok"));
        var service = new ChatBotService(client.Object);

        var many = Enumerable.Range(0, 30).Select(i => User($"msg {i}")).ToArray();
        await service.AskAsync(many);

        // system prompt + a capped number of recent turns (well under the 30 sent).
        Assert.NotNull(captured);
        Assert.True(captured!.Count < 30);
        Assert.Equal("system", captured[0].Role);
    }
}
