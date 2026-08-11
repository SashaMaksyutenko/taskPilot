using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Whiteboard;

namespace Taskpilot.API.Services;

/// <summary>
/// Authoritative CRUD for a project's whiteboard notes. Enforces project access on reads/writes and
/// — the point of moving this off the CRDT relay — restricts deletion to the note's author or the
/// project owner. Mutations broadcast to the project's board group over <c>WhiteboardHub</c>.
/// </summary>
public interface IWhiteboardService
{
    Task<Result<List<WhiteboardNoteDto>>> GetNotesAsync(Guid userId, Guid projectId);
    Task<Result<WhiteboardNoteDto>> CreateAsync(Guid userId, Guid projectId, CreateNoteDto dto);
    Task<Result<WhiteboardNoteDto>> UpdateAsync(Guid userId, Guid noteId, UpdateNoteDto dto);
    Task<Result> DeleteAsync(Guid userId, Guid noteId);
}
