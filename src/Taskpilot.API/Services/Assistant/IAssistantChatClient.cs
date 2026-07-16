using Taskpilot.API.Common;

namespace Taskpilot.API.Services.Assistant;

/// <summary>A function the model may call, with a JSON-schema parameter definition.</summary>
public sealed record ToolDefinition(string Name, string Description, object ParametersSchema);

/// <summary>A function call the model asked us to make.</summary>
public sealed record ToolCall(string Id, string Name, string Arguments);

/// <summary>
/// One message in an agent conversation. Beyond plain user/assistant/system text it can
/// carry the model's tool calls (assistant) or a tool's result (role "tool").
/// </summary>
public sealed class AgentMessage
{
    public required string Role { get; init; } // system | user | assistant | tool
    public string? Content { get; init; }
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; } // when the assistant calls tools
    public string? ToolCallId { get; init; } // when this message is a tool result

    public static AgentMessage System(string content) => new() { Role = "system", Content = content };
    public static AgentMessage User(string content) => new() { Role = "user", Content = content };
    public static AgentMessage Assistant(string? content) => new() { Role = "assistant", Content = content };
    public static AgentMessage AssistantWithCalls(string? content, IReadOnlyList<ToolCall> calls) =>
        new() { Role = "assistant", Content = content, ToolCalls = calls };
    public static AgentMessage Tool(string toolCallId, string content) =>
        new() { Role = "tool", ToolCallId = toolCallId, Content = content };
}

/// <summary>One reply from the model: either final text, or a set of tool calls to run.</summary>
public sealed record AssistantTurn(string? Content, IReadOnlyList<ToolCall> ToolCalls);

/// <summary>
/// Chat client that supports tool (function) calling. The real implementation talks to
/// OpenAI; tests provide a stub. Disabled (fails) when no API key is configured.
/// </summary>
public interface IAssistantChatClient
{
    bool IsEnabled { get; }

    /// <summary>Sends the conversation and available tools; returns the model's next turn.</summary>
    Task<Result<AssistantTurn>> CompleteAsync(IReadOnlyList<AgentMessage> messages, IReadOnlyList<ToolDefinition> tools);
}
