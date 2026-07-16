using Taskpilot.API.Common;
using Taskpilot.API.DTOs.ChatBot;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// Runs the tool-calling loop: sends the conversation plus the available tools to the
/// model, executes any tools it asks for (scoped to the user), feeds the results back,
/// and returns the model's final plain-language answer.
/// </summary>
public class AssistantAgent : IAssistantAgent
{
    // Cap the tool round-trips so a misbehaving model can't loop forever.
    private const int MaxIterations = 4;
    private const int MaxTurns = 24;

    private static readonly string SystemPrompt =
        "You are the TaskPilot assistant, a co-pilot for a team-collaboration app (projects, " +
        "Kanban tasks, chat, forum, marketplace, calendar). You can call the provided tools to " +
        "look up the signed-in user's projects and their tasks — use them whenever a question " +
        "depends on their data instead of guessing. Overdue and upcoming-deadline tools cover every " +
        "project the user owns or belongs to (including tasks assigned to teammates, with the assignee " +
        "named); get_my_tasks covers only tasks assigned to the user personally. Answer concisely and " +
        "helpfully. If the tools return nothing relevant, say so plainly. Reply in the user's language.";

    private readonly IAssistantChatClient _client;
    private readonly IAssistantToolbox _toolbox;
    private readonly ILogger<AssistantAgent> _logger;

    public AssistantAgent(IAssistantChatClient client, IAssistantToolbox toolbox, ILogger<AssistantAgent> logger)
    {
        _client = client;
        _toolbox = toolbox;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _client.IsEnabled;

    /// <inheritdoc />
    public async Task<Result<string>> AskAsync(Guid userId, IReadOnlyList<ChatBotMessageDto> conversation)
    {
        if (!_client.IsEnabled)
            return Result<string>.Fail("The AI assistant is not configured.");
        if (conversation is null || conversation.Count == 0)
            return Result<string>.Fail("Message is required.");

        var messages = new List<AgentMessage> { AgentMessage.System($"{SystemPrompt} Today is {DateTime.UtcNow:yyyy-MM-dd} (UTC).") };
        foreach (var m in conversation.Where(m => !string.IsNullOrWhiteSpace(m.Content)).TakeLast(MaxTurns))
        {
            messages.Add(string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? AgentMessage.Assistant(m.Content.Trim())
                : AgentMessage.User(m.Content.Trim()));
        }

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var turn = await _client.CompleteAsync(messages, _toolbox.Definitions);
            if (!turn.Succeeded)
                return Result<string>.Fail(turn.Error!);

            // No tools requested → this is the final answer.
            if (turn.Value!.ToolCalls.Count == 0)
                return Result<string>.Ok(turn.Value.Content?.Trim() ?? string.Empty);

            // Record the assistant's tool calls, then run each and feed the results back.
            messages.Add(AgentMessage.AssistantWithCalls(turn.Value.Content, turn.Value.ToolCalls));
            foreach (var call in turn.Value.ToolCalls)
            {
                var result = await _toolbox.ExecuteAsync(userId, call.Name, call.Arguments);
                messages.Add(AgentMessage.Tool(call.Id, result));
                _logger.LogInformation("Assistant tool call. Tool: {Tool}, UserId: {UserId}", call.Name, userId);
            }
        }

        return Result<string>.Fail("The assistant took too many steps. Please rephrase your question.");
    }
}
