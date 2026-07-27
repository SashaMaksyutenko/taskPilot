using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Reviews;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;
using Taskpilot.Contracts;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class ReviewService : IReviewService
{
    private readonly TaskpilotDbContext _context;
    private readonly INotificationService _notifications;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(TaskpilotDbContext context, INotificationService notifications, ILogger<ReviewService> logger)
    {
        _context = context;
        _notifications = notifications;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<UserReviewDto>> LeaveProjectReviewAsync(Guid raterId, Guid projectId, LeaveReviewDto dto)
    {
        if (dto.Stars < 1 || dto.Stars > 5)
            return Result<UserReviewDto>.Fail("Rating must be between 1 and 5 stars.");
        if (dto.RateeId == raterId)
            return Result<UserReviewDto>.Fail("You cannot review yourself.");

        var project = await _context.Projects
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Name, Archived = p.ArchivedAt != null })
            .FirstOrDefaultAsync();
        if (project is null)
            return Result<UserReviewDto>.Fail("Project not found.");
        if (project.Archived)
            return Result<UserReviewDto>.Fail("You cannot review members of an archived project.");

        // Both parties must belong to the project (as owner or member).
        if (!await IsProjectParticipantAsync(projectId, raterId))
            return Result<UserReviewDto>.Fail("You are not a member of this project.");
        if (!await IsProjectParticipantAsync(projectId, dto.RateeId))
            return Result<UserReviewDto>.Fail("That user is not a member of this project.");

        var alreadyReviewed = await _context.Reviews.AnyAsync(r =>
            r.Context == ReviewContext.Project && r.ContextId == projectId
            && r.RaterId == raterId && r.RateeId == dto.RateeId);
        if (alreadyReviewed)
            return Result<UserReviewDto>.Fail("You have already reviewed this member in this project.");

        var review = new Review
        {
            Id = Guid.NewGuid(),
            Context = ReviewContext.Project,
            ContextId = projectId,
            MarketplaceTaskId = null,
            RaterId = raterId,
            RateeId = dto.RateeId,
            Stars = dto.Stars,
            Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        await _notifications.CreateAsync(
            dto.RateeId,
            NotificationType.General,
            $"You received a {dto.Stars}★ review for your work on \"{project.Name}\".",
            $"/projects/{projectId}");

        _logger.LogInformation(
            "Project review left. ProjectId: {ProjectId}, RaterId: {RaterId}, RateeId: {RateeId}, Stars: {Stars}",
            projectId, raterId, dto.RateeId, dto.Stars);

        var rater = await _context.Users
            .Where(u => u.Id == raterId)
            .Select(u => new { u.Name, u.AvatarFileId })
            .FirstOrDefaultAsync();

        return Result<UserReviewDto>.Ok(new UserReviewDto
        {
            Id = review.Id,
            Context = ReviewContext.Project.ToString(),
            ContextId = projectId,
            ContextLabel = project.Name,
            ContextLink = $"/projects/{projectId}",
            RaterId = raterId,
            RaterName = rater?.Name ?? string.Empty,
            RaterAvatarUrl = UserMapper.AvatarUrl(raterId, rater?.AvatarFileId),
            Stars = review.Stars,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt,
        });
    }

    /// <inheritdoc />
    public async Task<Result<List<UserReviewDto>>> GetUserReviewsAsync(Guid userId)
    {
        var rows = await _context.Reviews
            .Where(r => r.RateeId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Context,
                r.ContextId,
                r.RaterId,
                Rater = _context.Users.Where(u => u.Id == r.RaterId).Select(u => new { u.Name, u.AvatarFileId }).FirstOrDefault(),
                r.Stars,
                r.Comment,
                r.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        var labels = await ResolveContextLabelsAsync(rows.Select(r => (r.Context, r.ContextId)));

        var reviews = rows.Select(r =>
        {
            var (label, link) = labels.GetValueOrDefault((r.Context, r.ContextId), (null, null));
            return new UserReviewDto
            {
                Id = r.Id,
                Context = r.Context.ToString(),
                ContextId = r.ContextId,
                ContextLabel = label,
                ContextLink = link,
                RaterId = r.RaterId,
                RaterName = r.Rater?.Name ?? string.Empty,
                RaterAvatarUrl = UserMapper.AvatarUrl(r.RaterId, r.Rater?.AvatarFileId),
                Stars = r.Stars,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
            };
        }).ToList();

        return Result<List<UserReviewDto>>.Ok(reviews);
    }

    private async Task<bool> IsProjectParticipantAsync(Guid projectId, Guid userId) =>
        await _context.Projects.AnyAsync(p => p.Id == projectId
            && (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)));

    /// <summary>
    /// Resolves each (context, id) pair to a display label and in-app link by looking up the
    /// scoping entity's name in its own table. Done in one query per context type.
    /// </summary>
    private async Task<Dictionary<(ReviewContext, Guid?), (string? Label, string? Link)>> ResolveContextLabelsAsync(
        IEnumerable<(ReviewContext Context, Guid? ContextId)> pairs)
    {
        var result = new Dictionary<(ReviewContext, Guid?), (string?, string?)>();

        var byContext = pairs
            .Where(p => p.ContextId is not null)
            .GroupBy(p => p.Context)
            .ToDictionary(g => g.Key, g => g.Select(p => p.ContextId!.Value).Distinct().ToList());

        if (byContext.TryGetValue(ReviewContext.Marketplace, out var taskIds))
        {
            var names = await _context.MarketplaceTasks
                .Where(t => taskIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Title })
                .ToDictionaryAsync(t => t.Id, t => t.Title);
            foreach (var id in taskIds)
                result[(ReviewContext.Marketplace, id)] = (names.GetValueOrDefault(id), $"/marketplace/{id}");
        }

        if (byContext.TryGetValue(ReviewContext.Project, out var projectIds))
        {
            var names = await _context.Projects
                .Where(p => projectIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name);
            foreach (var id in projectIds)
                result[(ReviewContext.Project, id)] = (names.GetValueOrDefault(id), $"/projects/{id}");
        }

        if (byContext.TryGetValue(ReviewContext.Forum, out var topicIds))
        {
            var names = await _context.ForumTopics
                .Where(t => topicIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Title })
                .ToDictionaryAsync(t => t.Id, t => t.Title);
            foreach (var id in topicIds)
                result[(ReviewContext.Forum, id)] = (names.GetValueOrDefault(id), $"/forum/{id}");
        }

        return result;
    }
}
