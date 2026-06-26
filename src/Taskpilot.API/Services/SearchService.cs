using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Search;

namespace Taskpilot.API.Services;

/// <summary>
/// Global search. Projects/tasks are scoped to the caller (own data only); forum
/// topics and users are public. Uses case-insensitive ILike (PostgreSQL).
/// </summary>
public class SearchService : ISearchService
{
    private const int PerCategory = 5;

    private readonly TaskpilotDbContext _context;

    public SearchService(TaskpilotDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Result<SearchResultsDto>> SearchAsync(Guid userId, string query)
    {
        var term = query.Trim();
        // Require at least 2 characters to avoid huge result sets.
        if (term.Length < 2)
            return Result<SearchResultsDto>.Ok(new SearchResultsDto());

        var pattern = $"%{term}%";

        var projects = await _context.Projects
            .Where(p => p.OwnerId == userId && EF.Functions.ILike(p.Name, pattern))
            .OrderBy(p => p.Name)
            .Take(PerCategory)
            .Select(p => new SearchItemDto { Id = p.Id, Label = p.Name })
            .AsNoTracking()
            .ToListAsync();

        var tasks = await _context.ProjectTasks
            .Where(t => t.Project.OwnerId == userId && EF.Functions.ILike(t.Title, pattern))
            .OrderByDescending(t => t.CreatedAt)
            .Take(PerCategory)
            // Tasks link to their project's board, so the navigation id is the project id.
            .Select(t => new SearchItemDto { Id = t.ProjectId, Label = t.Title, Sublabel = t.Project.Name })
            .AsNoTracking()
            .ToListAsync();

        var topics = await _context.ForumTopics
            .Where(t => EF.Functions.ILike(t.Title, pattern))
            .OrderByDescending(t => t.CreatedAt)
            .Take(PerCategory)
            .Select(t => new SearchItemDto { Id = t.Id, Label = t.Title, Sublabel = t.Author.Name })
            .AsNoTracking()
            .ToListAsync();

        var users = await _context.Users
            .Where(u => u.IsActive && u.Id != userId
                        && (EF.Functions.ILike(u.Name, pattern) || EF.Functions.ILike(u.Email, pattern)))
            .OrderBy(u => u.Name)
            .Take(PerCategory)
            .Select(u => new SearchItemDto { Id = u.Id, Label = u.Name, Sublabel = u.Title })
            .AsNoTracking()
            .ToListAsync();

        return Result<SearchResultsDto>.Ok(new SearchResultsDto
        {
            Projects = projects,
            Tasks = tasks,
            Topics = topics,
            Users = users,
        });
    }
}
