using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Gif;

namespace Taskpilot.API.Services;

/// <summary>
/// Searches a GIF provider (Giphy or Tenor). The real implementation calls the
/// provider over HTTP; disabled (returns empty) when no API key is configured.
/// </summary>
public interface IGifClient
{
    /// <summary>True only when an API key is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns GIFs matching the query, or trending GIFs when the query is empty.
    /// </summary>
    Task<Result<List<GifDto>>> SearchAsync(string? query, int limit);
}
