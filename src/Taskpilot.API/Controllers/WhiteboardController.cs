using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Whiteboard;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Authoritative CRUD for a project's whiteboard sticky notes. Deletion is restricted server-side to
/// the note's author or the project owner; realtime updates ride on <c>WhiteboardHub</c>.
/// </summary>
[ApiController]
[Authorize]
public class WhiteboardController : BaseApiController
{
    private readonly IWhiteboardService _whiteboard;
    private readonly IBillingService _billing;

    public WhiteboardController(IWhiteboardService whiteboard, IBillingService billing)
    {
        _whiteboard = whiteboard;
        _billing = billing;
    }

    /// <summary>All notes on a project's whiteboard.</summary>
    [HttpGet("api/projects/{projectId:guid}/whiteboard/notes")]
    public async Task<IActionResult> GetNotes(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _whiteboard.GetNotesAsync(userId.Value, projectId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Creates a note (authored by the current user).</summary>
    [HttpPost("api/projects/{projectId:guid}/whiteboard/notes")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateNoteDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        if (!await _billing.IsProAsync())
            return StatusCode(StatusCodes.Status402PaymentRequired, new { error = "The whiteboard is a Pro feature. Upgrade to Pro to use it." });

        var result = await _whiteboard.CreateAsync(userId.Value, projectId, dto);
        return result.Succeeded ? Ok(result.Value) : Forbid();
    }

    /// <summary>Moves, recolours or edits a note's text.</summary>
    [HttpPut("api/whiteboard/notes/{noteId:guid}")]
    public async Task<IActionResult> Update(Guid noteId, [FromBody] UpdateNoteDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _whiteboard.UpdateAsync(userId.Value, noteId, dto);
        if (result.Succeeded) return Ok(result.Value);
        return result.Error == "Note not found." ? NotFound(new { error = result.Error }) : Forbid();
    }

    /// <summary>Deletes a note — only the author or the project owner may.</summary>
    [HttpDelete("api/whiteboard/notes/{noteId:guid}")]
    public async Task<IActionResult> Delete(Guid noteId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _whiteboard.DeleteAsync(userId.Value, noteId);
        if (result.Succeeded) return NoContent();
        return result.Error == "Note not found." ? NotFound(new { error = result.Error }) : Forbid();
    }
}
