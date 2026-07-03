using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Notes;

namespace Taskpilot.API.Services;

/// <summary>Personal notes: every operation is scoped to the owner.</summary>
public interface INoteService
{
    /// <summary>Lists the owner's notes (pinned first, then newest).</summary>
    Task<Result<List<NoteDto>>> GetMineAsync(Guid ownerId);

    /// <summary>Creates a note for the owner.</summary>
    Task<Result<NoteDto>> CreateAsync(Guid ownerId, SaveNoteDto dto);

    /// <summary>Updates the owner's note.</summary>
    Task<Result<NoteDto>> UpdateAsync(Guid ownerId, Guid noteId, SaveNoteDto dto);

    /// <summary>Deletes the owner's note.</summary>
    Task<Result> DeleteAsync(Guid ownerId, Guid noteId);

    /// <summary>Renders the owner's note as a PDF document.</summary>
    Task<Result<byte[]>> ExportPdfAsync(Guid ownerId, Guid noteId);
}
