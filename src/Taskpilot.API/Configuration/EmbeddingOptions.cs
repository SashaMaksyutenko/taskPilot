namespace Taskpilot.API.Configuration;

/// <summary>
/// Settings for the text-embedding provider used by semantic search, bound from the
/// "Embeddings" section. Kept SEPARATE from <see cref="OpenAiOptions"/> because the chat
/// assistant may run on a provider without an embeddings endpoint (e.g. Groq). Point this
/// at OpenAI (the default) and set the key in .env. An empty key disables semantic search.
/// </summary>
public class EmbeddingOptions
{
    /// <summary>API key. Empty disables semantic search (it falls back to keyword search).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Embedding model. Defaults to a small, inexpensive OpenAI model.</summary>
    public string Model { get; set; } = "text-embedding-3-small";

    /// <summary>Base URL of the OpenAI-compatible embeddings API.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>True only when an API key is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
