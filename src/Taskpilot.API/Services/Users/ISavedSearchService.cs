using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Search;

namespace Taskpilot.API.Services;

/// <summary>Stores and reads a user's saved search queries.</summary>
public interface ISavedSearchService
{
    /// <summary>Saves a search for the user (capped per user; name and query required).</summary>
    Task<Result<SavedSearchDto>> CreateAsync(Guid userId, CreateSavedSearchDto dto);

    /// <summary>Lists the user's saved searches (newest first).</summary>
    Task<Result<List<SavedSearchDto>>> GetMineAsync(Guid userId);

    /// <summary>Deletes one of the user's saved searches (owner only).</summary>
    Task<Result> DeleteAsync(Guid userId, Guid id);
}
