using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Bookmarks;

namespace Taskpilot.API.Services;

/// <summary>Business logic for a user's bookmarks (saved shortcuts).</summary>
public interface IBookmarkService
{
    /// <summary>
    /// Adds the bookmark if absent, or removes it if it already exists.
    /// Returns whether the entity is now bookmarked.
    /// </summary>
    Task<Result<bool>> ToggleAsync(Guid userId, ToggleBookmarkDto dto);

    /// <summary>Lists the user's bookmarks, newest first.</summary>
    Task<Result<List<BookmarkDto>>> GetMineAsync(Guid userId);

    /// <summary>Removes a bookmark by id (only the owner's own).</summary>
    Task<Result> DeleteAsync(Guid userId, Guid bookmarkId);
}
