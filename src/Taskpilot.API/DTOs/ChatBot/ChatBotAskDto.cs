namespace Taskpilot.API.DTOs.ChatBot;

/// <summary>One message in the assistant conversation sent from the client.</summary>
public class ChatBotMessageDto
{
    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = "user";

    /// <summary>Message text.</summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>Request body for asking the assistant: the running conversation.</summary>
public class ChatBotAskDto
{
    public List<ChatBotMessageDto> Messages { get; set; } = new();
}
