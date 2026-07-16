using Taskpilot.API.Common;
using Taskpilot.API.DTOs.ChatBot;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// The data-aware assistant: answers questions about the signed-in user's own tasks,
/// deadlines and projects by calling read-only tools, then replying in plain language.
/// </summary>
public interface IAssistantAgent
{
    /// <summary>True when the underlying LLM client is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>Answers the user's latest message, looking up their data via tools as needed.</summary>
    Task<Result<string>> AskAsync(Guid userId, IReadOnlyList<ChatBotMessageDto> conversation);
}
