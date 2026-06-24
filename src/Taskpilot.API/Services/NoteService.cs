using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Notes;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles personal-note business logic. A note is private to its owner: every read
/// and write is scoped to the owner so users only touch their own notes.
/// </summary>
public class NoteService : INoteService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<NoteService> _logger;

    public NoteService(TaskpilotDbContext context, ILogger<NoteService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Reusable projection Note -> NoteDto (runs in SQL).
    private static readonly Expression<Func<Note, NoteDto>> ToDto = n => new NoteDto
    {
        Id = n.Id,
        Title = n.Title,
        Content = n.Content,
        Color = n.Color,
        IsPinned = n.IsPinned,
        CreatedAt = n.CreatedAt,
        UpdatedAt = n.UpdatedAt,
    };

    /// <inheritdoc />
    public async Task<Result<List<NoteDto>>> GetMineAsync(Guid ownerId)
    {
        var notes = await _context.Notes
            .Where(n => n.OwnerId == ownerId)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
            .Select(ToDto)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<NoteDto>>.Ok(notes);
    }

    /// <inheritdoc />
    public async Task<Result<NoteDto>> CreateAsync(Guid ownerId, SaveNoteDto dto)
    {
        var validation = Validate(dto);
        if (validation is not null)
            return Result<NoteDto>.Fail(validation);

        var note = new Note
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = dto.Title.Trim(),
            Content = dto.Content.Trim(),
            Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim(),
            IsPinned = dto.IsPinned,
            CreatedAt = DateTime.UtcNow,
        };
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Note created. NoteId: {NoteId}, OwnerId: {OwnerId}", note.Id, ownerId);
        return Result<NoteDto>.Ok(await LoadDtoAsync(note.Id));
    }

    /// <inheritdoc />
    public async Task<Result<NoteDto>> UpdateAsync(Guid ownerId, Guid noteId, SaveNoteDto dto)
    {
        var validation = Validate(dto);
        if (validation is not null)
            return Result<NoteDto>.Fail(validation);

        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.OwnerId == ownerId);
        if (note is null)
            return Result<NoteDto>.Fail("Note not found.");

        note.Title = dto.Title.Trim();
        note.Content = dto.Content.Trim();
        note.Color = string.IsNullOrWhiteSpace(dto.Color) ? null : dto.Color.Trim();
        note.IsPinned = dto.IsPinned;
        note.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result<NoteDto>.Ok(await LoadDtoAsync(note.Id));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid ownerId, Guid noteId)
    {
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.OwnerId == ownerId);
        if (note is null)
            return Result.Fail("Note not found.");

        _context.Notes.Remove(note);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    // A note needs at least a title or some content.
    private static string? Validate(SaveNoteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) && string.IsNullOrWhiteSpace(dto.Content))
            return "A note must have a title or some content.";
        return null;
    }

    private async Task<NoteDto> LoadDtoAsync(Guid noteId) =>
        await _context.Notes.Where(n => n.Id == noteId).Select(ToDto).AsNoTracking().FirstAsync();
}
