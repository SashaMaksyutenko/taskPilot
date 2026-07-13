using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Search;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>Stores and reads a user's saved search queries.</summary>
public class SavedSearchService : ISavedSearchService
{
    // Keep the list small and useful; refuse further saves past this.
    private const int MaxPerUser = 20;

    private readonly TaskpilotDbContext _context;
    private readonly ILogger<SavedSearchService> _logger;

    public SavedSearchService(TaskpilotDbContext context, ILogger<SavedSearchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<SavedSearchDto>> CreateAsync(Guid userId, CreateSavedSearchDto dto)
    {
        var name = dto.Name.Trim();
        var query = dto.Query.Trim();
        if (name.Length == 0 || query.Length == 0)
            return Result<SavedSearchDto>.Fail("Name and query are required.");

        var count = await _context.SavedSearches.CountAsync(s => s.UserId == userId);
        if (count >= MaxPerUser)
            return Result<SavedSearchDto>.Fail($"You can save at most {MaxPerUser} searches.");

        var saved = new SavedSearch
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Query = query,
            CreatedAt = DateTime.UtcNow,
        };
        _context.SavedSearches.Add(saved);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Saved search created. UserId: {UserId}, Id: {Id}", userId, saved.Id);
        return Result<SavedSearchDto>.Ok(Map(saved));
    }

    /// <inheritdoc />
    public async Task<Result<List<SavedSearchDto>>> GetMineAsync(Guid userId)
    {
        var items = await _context.SavedSearches
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SavedSearchDto
            {
                Id = s.Id,
                Name = s.Name,
                Query = s.Query,
                CreatedAt = s.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        return Result<List<SavedSearchDto>>.Ok(items);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid userId, Guid id)
    {
        var saved = await _context.SavedSearches.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (saved is null)
            return Result.Fail("Saved search not found.");

        _context.SavedSearches.Remove(saved);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    private static SavedSearchDto Map(SavedSearch s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Query = s.Query,
        CreatedAt = s.CreatedAt,
    };
}
