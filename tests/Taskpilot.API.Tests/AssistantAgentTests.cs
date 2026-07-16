using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.DTOs.ChatBot;
using Taskpilot.API.Services.Assistant;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for the tool-calling assistant. The chat client is mocked so no OpenAI
/// call happens; the toolbox is a fake that records what the agent asked for.
/// </summary>
public class AssistantAgentTests
{
    private static ChatBotMessageDto User(string content) => new() { Role = "user", Content = content };

    /// <summary>A toolbox that returns a canned result and records the calls it received.</summary>
    private sealed class FakeToolbox : IAssistantToolbox
    {
        public List<(Guid userId, string tool, string args)> Calls { get; } = new();
        public string Result { get; set; } = "{\"count\":0}";

        public IReadOnlyList<ToolDefinition> Definitions { get; } = new[]
        {
            new ToolDefinition("get_overdue_tasks", "…", new { type = "object" }),
        };

        public Task<string> ExecuteAsync(Guid userId, string toolName, string argumentsJson)
        {
            Calls.Add((userId, toolName, argumentsJson));
            return Task.FromResult(Result);
        }
    }

    [Fact]
    public async Task NotConfigured_Fails()
    {
        var client = new Mock<IAssistantChatClient>();
        client.SetupGet(c => c.IsEnabled).Returns(false);
        var agent = new AssistantAgent(client.Object, new FakeToolbox(), NullLogger<AssistantAgent>.Instance);

        Assert.False(agent.IsEnabled);
        Assert.False((await agent.AskAsync(Guid.NewGuid(), new[] { User("hi") })).Succeeded);
    }

    [Fact]
    public async Task NoToolCall_ReturnsAnswerDirectly()
    {
        var client = new Mock<IAssistantChatClient>();
        client.SetupGet(c => c.IsEnabled).Returns(true);
        client.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<AgentMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>()))
            .ReturnsAsync(Result<AssistantTurn>.Ok(new AssistantTurn("You have no overdue tasks.", Array.Empty<ToolCall>())));
        var agent = new AssistantAgent(client.Object, new FakeToolbox(), NullLogger<AssistantAgent>.Instance);

        var result = await agent.AskAsync(Guid.NewGuid(), new[] { User("hi") });

        Assert.True(result.Succeeded);
        Assert.Equal("You have no overdue tasks.", result.Value);
    }

    [Fact]
    public async Task RunsToolThenReturnsFinalAnswer()
    {
        var userId = Guid.NewGuid();
        var toolbox = new FakeToolbox { Result = "{\"count\":2}" };

        var client = new Mock<IAssistantChatClient>();
        client.SetupGet(c => c.IsEnabled).Returns(true);
        // First call → the model asks for a tool; second call → it answers.
        client.SetupSequence(c => c.CompleteAsync(It.IsAny<IReadOnlyList<AgentMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>()))
            .ReturnsAsync(Result<AssistantTurn>.Ok(new AssistantTurn(
                null, new[] { new ToolCall("call_1", "get_overdue_tasks", "{}") })))
            .ReturnsAsync(Result<AssistantTurn>.Ok(new AssistantTurn("You have 2 overdue tasks.", Array.Empty<ToolCall>())));

        var agent = new AssistantAgent(client.Object, toolbox, NullLogger<AssistantAgent>.Instance);

        var result = await agent.AskAsync(userId, new[] { User("what's overdue?") });

        Assert.True(result.Succeeded);
        Assert.Equal("You have 2 overdue tasks.", result.Value);
        // The tool was executed once, scoped to the caller.
        var call = Assert.Single(toolbox.Calls);
        Assert.Equal(userId, call.userId);
        Assert.Equal("get_overdue_tasks", call.tool);
    }

    [Fact]
    public async Task FeedsToolResultBackToTheModel()
    {
        var toolbox = new FakeToolbox { Result = "{\"count\":2}" };
        IReadOnlyList<AgentMessage>? secondCall = null;

        var client = new Mock<IAssistantChatClient>();
        client.SetupGet(c => c.IsEnabled).Returns(true);
        var seq = 0;
        client.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<AgentMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>()))
            .ReturnsAsync((IReadOnlyList<AgentMessage> msgs, IReadOnlyList<ToolDefinition> _) =>
            {
                seq++;
                if (seq == 1)
                    return Result<AssistantTurn>.Ok(new AssistantTurn(null, new[] { new ToolCall("call_1", "get_overdue_tasks", "{}") }));
                secondCall = msgs;
                return Result<AssistantTurn>.Ok(new AssistantTurn("done", Array.Empty<ToolCall>()));
            });
        var agent = new AssistantAgent(client.Object, toolbox, NullLogger<AssistantAgent>.Instance);

        await agent.AskAsync(Guid.NewGuid(), new[] { User("q") });

        // The second request must include the tool's result as a "tool" message.
        Assert.NotNull(secondCall);
        var toolMsg = Assert.Single(secondCall!, m => m.Role == "tool");
        Assert.Equal("call_1", toolMsg.ToolCallId);
        Assert.Contains("count", toolMsg.Content);
    }

    [Fact]
    public async Task StopsAfterTooManyToolRounds()
    {
        var client = new Mock<IAssistantChatClient>();
        client.SetupGet(c => c.IsEnabled).Returns(true);
        // Always ask for a tool → never converges.
        client.Setup(c => c.CompleteAsync(It.IsAny<IReadOnlyList<AgentMessage>>(), It.IsAny<IReadOnlyList<ToolDefinition>>()))
            .ReturnsAsync(Result<AssistantTurn>.Ok(new AssistantTurn(null, new[] { new ToolCall("c", "get_overdue_tasks", "{}") })));
        var agent = new AssistantAgent(client.Object, new FakeToolbox(), NullLogger<AssistantAgent>.Instance);

        var result = await agent.AskAsync(Guid.NewGuid(), new[] { User("loop") });

        Assert.False(result.Succeeded);
    }
}
