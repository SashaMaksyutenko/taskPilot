namespace Taskpilot.API.DTOs.Gif;

/// <summary>A single GIF result as returned to clients.</summary>
public class GifDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>URL of the animated GIF to send.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>URL of a small preview to show in the picker grid.</summary>
    public string PreviewUrl { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>Result of a GIF search: whether the feature is on plus the matches.</summary>
public class GifSearchResult
{
    /// <summary>False when no API key is configured (client hides the GIF button).</summary>
    public bool Enabled { get; set; }

    public List<GifDto> Gifs { get; set; } = new();
}
