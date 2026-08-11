using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Whiteboard;
using Taskpilot.API.Hubs;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class WhiteboardService : IWhiteboardService
{
    private const int MaxTextLength = 2000;

    private readonly TaskpilotDbContext _context;
    private readonly IHubContext<WhiteboardHub> _hub;

    public WhiteboardService(TaskpilotDbContext context, IHubContext<WhiteboardHub> hub)
    {
        _context = context;
        _hub = hub;
    }

    /// <inheritdoc />
    public async Task<Result<List<WhiteboardNoteDto>>> GetNotesAsync(Guid userId, Guid projectId)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<List<WhiteboardNoteDto>>.Fail("Project not found.");

        var notes = await _context.WhiteboardNotes
            .Where(n => n.ProjectId == projectId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();
        return Result<List<WhiteboardNoteDto>>.Ok(notes.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<WhiteboardNoteDto>> CreateAsync(Guid userId, Guid projectId, CreateNoteDto dto)
    {
        if (!await ProjectAccess.CanWriteAsync(_context, projectId, userId))
            return Result<WhiteboardNoteDto>.Fail("You cannot edit this whiteboard.");

        var authorName = await _context.Users.Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync() ?? "Unknown";
        var note = new WhiteboardNote
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            X = dto.X,
            Y = dto.Y,
            Text = Clip(dto.Text),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? "#fde68a" : dto.Color.Trim(),
            AuthorId = userId,
            AuthorName = authorName,
        };
        _context.WhiteboardNotes.Add(note);
        await _context.SaveChangesAsync();

        var result = ToDto(note);
        await BroadcastUpsertAsync(projectId, result);
        return Result<WhiteboardNoteDto>.Ok(result);
    }

    /// <inheritdoc />
    public async Task<Result<WhiteboardNoteDto>> UpdateAsync(Guid userId, Guid noteId, UpdateNoteDto dto)
    {
        var note = await _context.WhiteboardNotes.FirstOrDefaultAsync(n => n.Id == noteId);
        if (note is null)
            return Result<WhiteboardNoteDto>.Fail("Note not found.");

        // Any writer may move/edit a note; deletion is the only author-gated action.
        if (!await ProjectAccess.CanWriteAsync(_context, note.ProjectId, userId))
            return Result<WhiteboardNoteDto>.Fail("You cannot edit this whiteboard.");

        if (dto.X is not null) note.X = dto.X.Value;
        if (dto.Y is not null) note.Y = dto.Y.Value;
        if (dto.Color is not null && !string.IsNullOrWhiteSpace(dto.Color)) note.Color = dto.Color.Trim();
        if (dto.Text is not null)
        {
            var text = Clip(dto.Text);
            if (text != note.Text)
            {
                note.Text = text;
                if (userId != note.AuthorId)
                {
                    note.EditedById = userId;
                    note.EditedByName = await _context.Users.Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync();
                }
            }
        }
        note.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var result = ToDto(note);
        await BroadcastUpsertAsync(note.ProjectId, result);
        return Result<WhiteboardNoteDto>.Ok(result);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid userId, Guid noteId)
    {
        var note = await _context.WhiteboardNotes.FirstOrDefaultAsync(n => n.Id == noteId);
        if (note is null)
            return Result.Fail("Note not found.");

        // THE ENFORCEMENT: only the note's author or the project owner may delete it.
        var isOwner = await _context.Projects.AnyAsync(p => p.Id == note.ProjectId && p.OwnerId == userId);
        if (note.AuthorId != userId && !isOwner)
            return Result.Fail("You can only delete your own notes.");

        _context.WhiteboardNotes.Remove(note);
        await _context.SaveChangesAsync();

        await _hub.Clients.Group(WhiteboardHub.GroupName(note.ProjectId)).SendAsync("NoteDeleted", noteId);
        return Result.Ok();
    }

    private Task BroadcastUpsertAsync(Guid projectId, WhiteboardNoteDto dto) =>
        _hub.Clients.Group(WhiteboardHub.GroupName(projectId)).SendAsync("NoteUpserted", dto);

    private static string Clip(string? text)
    {
        text ??= string.Empty;
        return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
    }

    private static WhiteboardNoteDto ToDto(WhiteboardNote n) => new()
    {
        Id = n.Id,
        X = n.X,
        Y = n.Y,
        Text = n.Text,
        Color = n.Color,
        AuthorId = n.AuthorId,
        AuthorName = n.AuthorName,
        EditedById = n.EditedById,
        EditedByName = n.EditedByName,
    };
}
