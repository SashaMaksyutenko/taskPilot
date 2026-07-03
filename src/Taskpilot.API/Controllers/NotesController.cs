using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Notes;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Personal notes for the current user. Every endpoint is scoped to the caller.
/// </summary>
[ApiController]
[Authorize]
[Route("api/notes")]
public class NotesController : BaseApiController
{
    private readonly INoteService _notes;

    public NotesController(INoteService notes)
    {
        _notes = notes;
    }

    /// <summary>Lists the current user's notes.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notes.GetMineAsync(userId.Value);
        return Ok(result.Value);
    }

    /// <summary>Creates a note.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveNoteDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notes.CreateAsync(userId.Value, dto);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Updates a note.</summary>
    [HttpPut("{noteId:guid}")]
    public async Task<IActionResult> Update(Guid noteId, [FromBody] SaveNoteDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notes.UpdateAsync(userId.Value, noteId, dto);
        return result.Succeeded
            ? Ok(result.Value)
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes a note.</summary>
    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> Delete(Guid noteId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notes.DeleteAsync(userId.Value, noteId);
        return result.Succeeded
            ? Ok(new { message = "Note deleted." })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>Exports a note as a PDF file.</summary>
    [HttpGet("{noteId:guid}/pdf")]
    public async Task<IActionResult> ExportPdf(Guid noteId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _notes.ExportPdfAsync(userId.Value, noteId);
        return result.Succeeded
            ? File(result.Value!, "application/pdf", $"note-{noteId}.pdf")
            : NotFound(new { error = result.Error });
    }
}
