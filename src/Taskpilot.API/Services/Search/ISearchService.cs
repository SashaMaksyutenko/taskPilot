using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Search;

namespace Taskpilot.API.Services;

/// <summary>Global search across the user's projects/tasks and public forum/users.</summary>
public interface ISearchService
{
    /// <summary>
    /// Searches projects and tasks owned by the caller, plus public forum topics and
    /// users, for the given term. Returns a small number of hits per category.
    /// </summary>
    Task<Result<SearchResultsDto>> SearchAsync(Guid userId, string query);
}
