using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Bookmarks;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>Handles creating, listing and removing a user's bookmarks.</summary>
public class BookmarkService : IBookmarkService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<BookmarkService> _logger;

    public BookmarkService(TaskpilotDbContext context, ILogger<BookmarkService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ToggleAsync(Guid userId, ToggleBookmarkDto dto)
    {
        // Validate the entity type.
        if (!Enum.TryParse<BookmarkType>(dto.Type, ignoreCase: true, out var type))
            return Result<bool>.Fail("Invalid bookmark type.");
        if (dto.EntityId == Guid.Empty)
            return Result<bool>.Fail("EntityId is required.");

        var existing = await _context.Bookmarks
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Type == type && b.EntityId == dto.EntityId);

        if (existing is not null)
        {
            // Second toggle removes it.
            _context.Bookmarks.Remove(existing);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Bookmark removed. UserId: {UserId}, Type: {Type}, EntityId: {EntityId}", userId, type, dto.EntityId);
            return Result<bool>.Ok(false);
        }

        _context.Bookmarks.Add(new Bookmark
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            EntityId = dto.EntityId,
            Title = (dto.Title ?? string.Empty).Trim(),
            Link = (dto.Link ?? string.Empty).Trim(),
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        _logger.LogInformation("Bookmark added. UserId: {UserId}, Type: {Type}, EntityId: {EntityId}", userId, type, dto.EntityId);
        return Result<bool>.Ok(true);
    }

    /// <inheritdoc />
    public async Task<Result<List<BookmarkDto>>> GetMineAsync(Guid userId)
    {
        var items = await _context.Bookmarks
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookmarkDto
            {
                Id = b.Id,
                Type = b.Type.ToString(),
                EntityId = b.EntityId,
                Title = b.Title,
                Link = b.Link,
                CreatedAt = b.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        return Result<List<BookmarkDto>>.Ok(items);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid userId, Guid bookmarkId)
    {
        var bookmark = await _context.Bookmarks
            .FirstOrDefaultAsync(b => b.Id == bookmarkId && b.UserId == userId);
        if (bookmark is null)
            return Result.Fail("Bookmark not found.");

        _context.Bookmarks.Remove(bookmark);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }
}
