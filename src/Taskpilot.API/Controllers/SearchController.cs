using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Search;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Global search across the current user's data and public content.</summary>
[ApiController]
[Authorize]
[Route("api/search")]
public class SearchController : BaseApiController
{
    private readonly ISearchService _search;
    private readonly ISavedSearchService _saved;

    public SearchController(ISearchService search, ISavedSearchService saved)
    {
        _search = search;
        _saved = saved;
    }

    /// <summary>Searches projects, tasks, forum topics and users for the query.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _search.SearchAsync(userId.Value, q ?? string.Empty);
        return Ok(result.Value);
    }

    /// <summary>Lists the current user's saved searches (newest first).</summary>
    [HttpGet("saved")]
    public async Task<IActionResult> GetSaved()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _saved.GetMineAsync(userId.Value);
        return Ok(result.Value);
    }

    /// <summary>Saves a search query for quick re-running.</summary>
    [HttpPost("saved")]
    public async Task<IActionResult> SaveSearch([FromBody] CreateSavedSearchDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _saved.CreateAsync(userId.Value, dto);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes one of the current user's saved searches.</summary>
    [HttpDelete("saved/{id:guid}")]
    public async Task<IActionResult> DeleteSaved(Guid id)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _saved.DeleteAsync(userId.Value, id);
        return result.Succeeded ? NoContent() : NotFound(new { error = result.Error });
    }
}
