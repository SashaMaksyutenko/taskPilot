using Taskpilot.API.Common;
using Taskpilot.API.DTOs.ChatBot;

namespace Taskpilot.API.Services;

/// <summary>
/// Assistant orchestration: prepends a system prompt that scopes the bot to helping
/// with TaskPilot, keeps only the recent turns, and calls the chat-completions client.
/// </summary>
public class ChatBotService : IChatBotService
{
    private const int MaxTurns = 24; // cap history sent to the model

    private const string SystemPrompt =
        "You are the TaskPilot assistant, a helpful in-app guide for a team collaboration " +
        "platform (projects, Kanban tasks, chat, forum, a task marketplace, calendar and " +
        "notifications). Answer concisely and help users get things done in TaskPilot. " +
        "If asked something unrelated, gently steer back to TaskPilot. Reply in the user's language.";

    private readonly IChatBotClient _client;

    public ChatBotService(IChatBotClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public bool IsEnabled => _client.IsEnabled;

    /// <inheritdoc />
    public async Task<Result<string>> AskAsync(IReadOnlyList<ChatBotMessageDto> conversation)
    {
        if (conversation is null || conversation.Count == 0)
            return Result<string>.Fail("Message is required.");

        // Keep only valid, non-empty turns, then the most recent ones.
        var turns = conversation
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new ChatBotMessage(NormalizeRole(m.Role), m.Content.Trim()))
            .TakeLast(MaxTurns)
            .ToList();

        if (turns.Count == 0)
            return Result<string>.Fail("Message is required.");

        var messages = new List<ChatBotMessage> { new("system", SystemPrompt) };
        messages.AddRange(turns);

        return await _client.CompleteAsync(messages);
    }

    // Only "user" and "assistant" are valid conversational roles here.
    private static string NormalizeRole(string? role) =>
        string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
}
