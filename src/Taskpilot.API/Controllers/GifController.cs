using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Gif;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// GIF search proxy for chat. Keeps the provider API key server-side. Returns an
/// empty (disabled) result when no key is configured, so the client hides the button.
/// </summary>
[ApiController]
[Authorize]
[Route("api/gifs")]
public class GifController : BaseApiController
{
    private readonly IGifClient _gifs;

    public GifController(IGifClient gifs)
    {
        _gifs = gifs;
    }

    /// <summary>Searches GIFs (or returns trending when the query is empty).</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q = null, [FromQuery] int limit = 40)
    {
        if (!_gifs.IsEnabled)
            return Ok(new GifSearchResult { Enabled = false });

        var result = await _gifs.SearchAsync(q, limit);
        return Ok(new GifSearchResult
        {
            Enabled = true,
            Gifs = result.Succeeded ? result.Value! : new List<GifDto>(),
        });
    }
}
