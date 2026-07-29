using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Read-only public access to a shared project board via its share token. No auth.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/boards")]
public class PublicBoardsController : ControllerBase
{
    private readonly IProjectService _projects;

    public PublicBoardsController(IProjectService projects)
    {
        _projects = projects;
    }

    /// <summary>Returns the shared board (name, colour and tasks) for a valid token, else 404.</summary>
    [HttpGet("{token}")]
    public async Task<IActionResult> Get(string token)
    {
        var result = await _projects.GetPublicBoardAsync(token);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
