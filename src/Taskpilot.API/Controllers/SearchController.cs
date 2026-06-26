using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Global search across the current user's data and public content.</summary>
[ApiController]
[Authorize]
[Route("api/search")]
public class SearchController : BaseApiController
{
    private readonly ISearchService _search;

    public SearchController(ISearchService search)
    {
        _search = search;
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
}
