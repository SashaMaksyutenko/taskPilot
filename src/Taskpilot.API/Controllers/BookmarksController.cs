using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Bookmarks;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>REST endpoints for the current user's bookmarks. All require authentication.</summary>
[ApiController]
[Authorize]
[Route("api/bookmarks")]
public class BookmarksController : BaseApiController
{
    private readonly IBookmarkService _bookmarks;

    public BookmarksController(IBookmarkService bookmarks)
    {
        _bookmarks = bookmarks;
    }

    /// <summary>Lists the current user's bookmarks (newest first).</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _bookmarks.GetMineAsync(userId.Value);
        return Ok(result.Value);
    }

    /// <summary>Adds or removes a bookmark; returns whether it is now bookmarked.</summary>
    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle([FromBody] ToggleBookmarkDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _bookmarks.ToggleAsync(userId.Value, dto);
        return result.Succeeded
            ? Ok(new { bookmarked = result.Value })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Removes a bookmark by id.</summary>
    [HttpDelete("{bookmarkId:guid}")]
    public async Task<IActionResult> Delete(Guid bookmarkId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _bookmarks.DeleteAsync(userId.Value, bookmarkId);
        return result.Succeeded
            ? Ok(new { message = "Bookmark removed." })
            : BadRequest(new { error = result.Error });
    }
}
