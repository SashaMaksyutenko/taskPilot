using Taskpilot.API.Common;

namespace Taskpilot.API.Services;

/// <summary>One message in an assistant conversation. Role is "system", "user" or "assistant".</summary>
public record ChatBotMessage(string Role, string Content);

/// <summary>
/// Talks to the LLM chat-completions API. The real implementation calls OpenAI; tests
/// provide a stub so the assistant logic can be verified without any network calls.
/// </summary>
public interface IChatBotClient
{
    /// <summary>True only when an API key is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>Returns the assistant's reply to the given conversation, or a failure.</summary>
    Task<Result<string>> CompleteAsync(IReadOnlyList<ChatBotMessage> messages);
}
