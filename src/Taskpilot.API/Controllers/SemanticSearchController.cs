using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services.Search;

namespace Taskpilot.API.Controllers;

/// <summary>Embedding-based semantic search over the caller's tasks and notes.</summary>
[ApiController]
[Authorize]
[Route("api/search/semantic")]
public class SemanticSearchController : BaseApiController
{
    private readonly ISemanticSearchService _semantic;

    public SemanticSearchController(ISemanticSearchService semantic)
    {
        _semantic = semantic;
    }

    /// <summary>Whether semantic search is enabled and how many items the caller has indexed.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _semantic.GetStatusAsync(userId.Value));
    }

    /// <summary>Ranks the caller's indexed items by semantic similarity to the query.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q = "", [FromQuery] int limit = 10)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _semantic.SearchAsync(userId.Value, q, limit);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Rebuilds the caller's semantic index from their current tasks and notes.</summary>
    [HttpPost("reindex")]
    public async Task<IActionResult> Reindex()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _semantic.ReindexAsync(userId.Value);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
