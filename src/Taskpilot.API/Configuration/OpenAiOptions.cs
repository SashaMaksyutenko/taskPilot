namespace Taskpilot.API.Configuration;

/// <summary>
/// OpenAI settings for the in-app AI assistant, bound from the "OpenAi" section.
/// The API key comes from .env — keep it out of source control. Empty key disables
/// the assistant.
/// </summary>
public class OpenAiOptions
{
    /// <summary>API key. Empty disables the assistant.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Chat model to use. Defaults to a small, inexpensive OpenAI model.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Base URL of the OpenAI-compatible API. Defaults to OpenAI; point it at another
    /// provider (e.g. Groq: https://api.groq.com/openai/v1) to use their models/key.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>True only when an API key is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
