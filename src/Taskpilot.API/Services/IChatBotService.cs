using Taskpilot.API.Common;
using Taskpilot.API.DTOs.ChatBot;

namespace Taskpilot.API.Services;

/// <summary>The in-app AI assistant: answers user questions about using TaskPilot.</summary>
public interface IChatBotService
{
    /// <summary>True when the assistant is configured and available.</summary>
    bool IsEnabled { get; }

    /// <summary>Answers the user's latest message given the running conversation.</summary>
    Task<Result<string>> AskAsync(IReadOnlyList<ChatBotMessageDto> conversation);
}
