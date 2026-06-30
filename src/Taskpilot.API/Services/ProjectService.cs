using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles project business logic. A project is private to its owner: every read
/// and write is scoped to the owner so users only touch their own projects.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly TaskpilotDbContext _context;
    private readonly IWebhookService _webhooks;
    private readonly INotificationService _notifications;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        TaskpilotDbContext context,
        IWebhookService webhooks,
        INotificationService notifications,
        ILogger<ProjectService> logger)
    {
        _context = context;
        _webhooks = webhooks;
        _notifications = notifications;
        _logger = logger;
    }

    // Reusable projection Project -> ProjectDto (runs in SQL).
    private static readonly Expression<Func<Project, ProjectDto>> ToDto = p => new ProjectDto
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Color = p.Color,
        OwnerId = p.OwnerId,
        OwnerName = p.Owner.Name,
        TaskCount = p.Tasks.Count,
        CompletedTaskCount = p.Tasks.Count(t => t.Status == ProjectTaskStatus.Done),
        MemberCount = p.Members.Count,
        IsArchived = p.ArchivedAt != null,
        CreatedAt = p.CreatedAt,
        ArchivedAt = p.ArchivedAt,
    };

    /// <inheritdoc />
    public async Task<Result<ProjectDto>> CreateProjectAsync(Guid ownerId, SaveProjectDto dto)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Color = dto.Color?.Trim(),
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
        };
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        await _webhooks.DispatchAsync(WebhookEvents.ProjectCreated, new
        {
            projectId = project.Id,
            name = project.Name,
            ownerId,
        });

        _logger.LogInformation("Project created. ProjectId: {ProjectId}, OwnerId: {OwnerId}", project.Id, ownerId);
        return Result<ProjectDto>.Ok(await LoadDtoAsync(project.Id));
    }

    /// <inheritdoc />
    public async Task<Result<List<ProjectDto>>> GetProjectsAsync(Guid ownerId, bool includeArchived)
    {
        // Projects the user owns or collaborates on.
        var projects = await _context.Projects
            .Where(p => (p.OwnerId == ownerId || p.Members.Any(m => m.UserId == ownerId))
                        && (includeArchived || p.ArchivedAt == null))
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToDto)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<ProjectDto>>.Ok(projects);
    }

    /// <inheritdoc />
    public async Task<Result<ProjectDto>> GetProjectAsync(Guid projectId, Guid userId)
    {
        var dto = await _context.Projects
            .Where(p => p.Id == projectId && (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)))
            .Select(ToDto)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return dto is null
            ? Result<ProjectDto>.Fail("Project not found.")
            : Result<ProjectDto>.Ok(dto);
    }

    /// <inheritdoc />
    public async Task<Result<ProjectDto>> UpdateProjectAsync(Guid ownerId, Guid projectId, SaveProjectDto dto)
    {
        var project = await GetOwnedAsync(projectId, ownerId);
        if (project is null)
            return Result<ProjectDto>.Fail("Project not found.");

        project.Name = dto.Name.Trim();
        project.Description = dto.Description?.Trim();
        project.Color = dto.Color?.Trim();
        await _context.SaveChangesAsync();

        return Result<ProjectDto>.Ok(await LoadDtoAsync(project.Id));
    }

    /// <inheritdoc />
    public async Task<Result> SetArchivedAsync(Guid ownerId, Guid projectId, bool archived)
    {
        var project = await GetOwnedAsync(projectId, ownerId);
        if (project is null)
            return Result.Fail("Project not found.");

        project.ArchivedAt = archived ? DateTime.UtcNow : null;
        await _context.SaveChangesAsync();

        // Only the archive transition is broadcast (not restore).
        if (archived)
            await _webhooks.DispatchAsync(WebhookEvents.ProjectArchived, new
            {
                projectId = project.Id,
                name = project.Name,
                archivedAt = project.ArchivedAt,
            });

        _logger.LogInformation("Project {State}. ProjectId: {ProjectId}", archived ? "archived" : "restored", projectId);
        return Result.Ok();
    }

    // --- helpers ---

    /// <summary>Loads a tracked project only if it belongs to the given owner.</summary>
    private Task<Project?> GetOwnedAsync(Guid projectId, Guid ownerId) =>
        _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == ownerId);

    /// <summary>Reloads a project as a DTO (with owner name and task count).</summary>
    private async Task<ProjectDto> LoadDtoAsync(Guid projectId) =>
        await _context.Projects.Where(p => p.Id == projectId).Select(ToDto).AsNoTracking().FirstAsync();

    /// <inheritdoc />
    public async Task<Result<List<ProjectMemberDto>>> GetMembersAsync(Guid userId, Guid projectId)
    {
        var project = await _context.Projects
            .Include(p => p.Owner)
            .Include(p => p.Members).ThenInclude(m => m.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);

        // Only the owner or a member may view the roster.
        if (project is null || (project.OwnerId != userId && project.Members.All(m => m.UserId != userId)))
            return Result<List<ProjectMemberDto>>.Fail("Project not found.");

        var members = new List<ProjectMemberDto>
        {
            new()
            {
                UserId = project.OwnerId,
                Name = project.Owner.Name,
                AvatarUrl = UserMapper.AvatarUrl(project.Owner),
                Role = nameof(ProjectMemberRole.Editor),
                IsOwner = true,
            },
        };
        members.AddRange(project.Members
            .OrderBy(m => m.User.Name)
            .Select(m => new ProjectMemberDto
            {
                UserId = m.UserId,
                Name = m.User.Name,
                AvatarUrl = UserMapper.AvatarUrl(m.User),
                Role = m.Role.ToString(),
                IsOwner = false,
            }));

        return Result<List<ProjectMemberDto>>.Ok(members);
    }

    /// <inheritdoc />
    public async Task<Result<ProjectMemberDto>> AddMemberAsync(Guid ownerId, Guid projectId, Guid targetUserId, string? role)
    {
        // Only the owner manages members.
        var project = await GetOwnedAsync(projectId, ownerId);
        if (project is null)
            return Result<ProjectMemberDto>.Fail("Project not found.");

        if (targetUserId == ownerId)
            return Result<ProjectMemberDto>.Fail("The owner already has access.");

        // Default to Editor; only "Viewer" downgrades.
        var memberRole = Enum.TryParse<ProjectMemberRole>(role, ignoreCase: true, out var parsed)
            ? parsed
            : ProjectMemberRole.Editor;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (user is null)
            return Result<ProjectMemberDto>.Fail("User not found.");

        var already = await _context.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == targetUserId);
        if (already)
            return Result<ProjectMemberDto>.Fail("This user is already a member.");

        _context.ProjectMembers.Add(new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = targetUserId,
            Role = memberRole,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        // Let the new collaborator know (in-app + real-time via the hub).
        await _notifications.CreateAsync(
            targetUserId,
            NotificationType.General,
            $"You were added to the project \"{project.Name}\".",
            $"/projects/{projectId}");

        _logger.LogInformation("Member added. ProjectId: {ProjectId}, UserId: {UserId}, Role: {Role}", projectId, targetUserId, memberRole);
        return Result<ProjectMemberDto>.Ok(new ProjectMemberDto
        {
            UserId = user.Id,
            Name = user.Name,
            AvatarUrl = UserMapper.AvatarUrl(user),
            Role = memberRole.ToString(),
            IsOwner = false,
        });
    }

    /// <inheritdoc />
    public async Task<Result<ProjectMemberDto>> SetMemberRoleAsync(Guid ownerId, Guid projectId, Guid targetUserId, string role)
    {
        if (!Enum.TryParse<ProjectMemberRole>(role, ignoreCase: true, out var memberRole))
            return Result<ProjectMemberDto>.Fail("Invalid role.");

        // Only the owner manages members.
        var owns = await _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == ownerId);
        if (!owns)
            return Result<ProjectMemberDto>.Fail("Project not found.");

        var membership = await _context.ProjectMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == targetUserId);
        if (membership is null)
            return Result<ProjectMemberDto>.Fail("Member not found.");

        membership.Role = memberRole;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Member role changed. ProjectId: {ProjectId}, UserId: {UserId}, Role: {Role}", projectId, targetUserId, memberRole);
        return Result<ProjectMemberDto>.Ok(new ProjectMemberDto
        {
            UserId = membership.UserId,
            Name = membership.User.Name,
            AvatarUrl = UserMapper.AvatarUrl(membership.User),
            Role = memberRole.ToString(),
            IsOwner = false,
        });
    }

    /// <inheritdoc />
    public async Task<Result> RemoveMemberAsync(Guid ownerId, Guid projectId, Guid targetUserId)
    {
        // Only the owner manages members.
        var project = await GetOwnedAsync(projectId, ownerId);
        if (project is null)
            return Result.Fail("Project not found.");

        var membership = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == targetUserId);
        if (membership is null)
            return Result.Fail("Member not found.");

        _context.ProjectMembers.Remove(membership);
        await _context.SaveChangesAsync();

        // Tell the removed user (they no longer have access, so link to the projects list).
        await _notifications.CreateAsync(
            targetUserId,
            NotificationType.General,
            $"You were removed from the project \"{project.Name}\".",
            "/projects");

        _logger.LogInformation("Member removed. ProjectId: {ProjectId}, UserId: {UserId}", projectId, targetUserId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> LeaveAsync(Guid userId, Guid projectId)
    {
        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null)
            return Result.Fail("Project not found.");

        // The owner manages the project; they archive/delete rather than leave.
        if (project.OwnerId == userId)
            return Result.Fail("The owner cannot leave their own project.");

        var membership = await _context.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);
        if (membership is null)
            return Result.Fail("You are not a member of this project.");

        _context.ProjectMembers.Remove(membership);
        await _context.SaveChangesAsync();

        // Let the owner know a collaborator left.
        var userName = await _context.Users.Where(u => u.Id == userId).Select(u => u.Name).FirstAsync();
        await _notifications.CreateAsync(
            project.OwnerId,
            NotificationType.General,
            $"{userName} left the project \"{project.Name}\".",
            $"/projects/{projectId}");

        _logger.LogInformation("Member left. ProjectId: {ProjectId}, UserId: {UserId}", projectId, userId);
        return Result.Ok();
    }
}
