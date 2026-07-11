namespace Taskpilot.API.Configuration;

/// <summary>
/// GIF search settings, bound from the "Gif" section. The API key comes from .env —
/// keep it out of source control. Empty key disables GIFs in chat.
/// </summary>
public class GifOptions
{
    /// <summary>Provider: "Giphy" (default) or "Tenor".</summary>
    public string Provider { get; set; } = "Giphy";

    /// <summary>API key. Empty disables GIF search.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Content rating filter (Giphy): g, pg, pg-18, r. Defaults to pg-18.</summary>
    public string Rating { get; set; } = "pg-18";

    /// <summary>True only when an API key is configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
