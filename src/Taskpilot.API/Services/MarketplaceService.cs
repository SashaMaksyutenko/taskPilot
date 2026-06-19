using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Marketplace;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles marketplace business logic: posting tasks, browsing them, applying,
/// and accepting/rejecting applications.
/// </summary>
public class MarketplaceService : IMarketplaceService
{
    private readonly TaskpilotDbContext _context;
    private readonly ILogger<MarketplaceService> _logger;

    public MarketplaceService(TaskpilotDbContext context, ILogger<MarketplaceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<TaskDetailDto>> CreateTaskAsync(Guid posterId, CreateTaskDto dto)
    {
        _logger.LogInformation("CreateTask. PosterId: {PosterId}", posterId);

        try
        {
            var task = new MarketplaceTask
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                Description = dto.Description.Trim(),
                Budget = dto.Budget,
                RequiredSkills = dto.RequiredSkills?.Trim(),
                Deadline = dto.Deadline,
                Status = MarketplaceTaskStatus.Open,
                PosterId = posterId,
                CreatedAt = DateTime.UtcNow,
            };
            _context.MarketplaceTasks.Add(task);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Task posted. TaskId: {TaskId}", task.Id);
            return await GetTaskAsync(task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting task. PosterId: {PosterId}", posterId);
            return Result<TaskDetailDto>.Fail("An unexpected error occurred.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<TaskListItemDto>>> GetTasksAsync()
    {
        var tasks = await _context.MarketplaceTasks
            .OrderBy(t => t.Status)          // Open (0) first
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new TaskListItemDto
            {
                Id = t.Id,
                Title = t.Title,
                Budget = t.Budget,
                RequiredSkills = t.RequiredSkills,
                Deadline = t.Deadline,
                Status = t.Status.ToString(),
                PosterId = t.PosterId,
                PosterName = t.Poster.Name,
                ApplicationCount = t.Applications.Count,
                CreatedAt = t.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        return Result<List<TaskListItemDto>>.Ok(tasks);
    }

    /// <inheritdoc />
    public async Task<Result<TaskDetailDto>> GetTaskAsync(Guid taskId)
    {
        var task = await _context.MarketplaceTasks
            .Include(t => t.Poster)
            .Include(t => t.Assignee)
            .Include(t => t.Applications).ThenInclude(a => a.Applicant)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task is null)
            return Result<TaskDetailDto>.Fail("Task not found.");

        return Result<TaskDetailDto>.Ok(MapDetail(task));
    }

    /// <inheritdoc />
    public async Task<Result<ApplicationDto>> ApplyAsync(Guid applicantId, ApplyDto dto)
    {
        _logger.LogInformation("Apply. TaskId: {TaskId}, ApplicantId: {ApplicantId}", dto.TaskId, applicantId);

        var task = await _context.MarketplaceTasks.FirstOrDefaultAsync(t => t.Id == dto.TaskId);
        if (task is null)
            return Result<ApplicationDto>.Fail("Task not found.");

        if (task.Status != MarketplaceTaskStatus.Open)
            return Result<ApplicationDto>.Fail("This task is not open for applications.");

        if (task.PosterId == applicantId)
            return Result<ApplicationDto>.Fail("You cannot apply to your own task.");

        var alreadyApplied = await _context.TaskApplications
            .AnyAsync(a => a.TaskId == dto.TaskId && a.ApplicantId == applicantId);
        if (alreadyApplied)
            return Result<ApplicationDto>.Fail("You have already applied to this task.");

        try
        {
            var application = new TaskApplication
            {
                Id = Guid.NewGuid(),
                TaskId = dto.TaskId,
                ApplicantId = applicantId,
                CoverLetter = dto.CoverLetter.Trim(),
                ProposedRate = dto.ProposedRate,
                Status = ApplicationStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };
            _context.TaskApplications.Add(application);
            await _context.SaveChangesAsync();

            var applicantName = await _context.Users
                .Where(u => u.Id == applicantId)
                .Select(u => u.Name)
                .FirstAsync();

            _logger.LogInformation("Application submitted. ApplicationId: {ApplicationId}", application.Id);
            return Result<ApplicationDto>.Ok(MapApplication(application, applicantName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying. TaskId: {TaskId}", dto.TaskId);
            return Result<ApplicationDto>.Fail("An unexpected error occurred.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> DecideApplicationAsync(Guid posterId, Guid applicationId, bool accept)
    {
        var application = await _context.TaskApplications
            .Include(a => a.Task).ThenInclude(t => t.Applications)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application is null)
            return Result.Fail("Application not found.");

        var task = application.Task;
        if (task.PosterId != posterId)
            return Result.Fail("Only the task poster can decide on applications.");

        if (application.Status != ApplicationStatus.Pending)
            return Result.Fail("This application has already been decided.");

        if (accept)
        {
            application.Status = ApplicationStatus.Accepted;
            task.AssigneeId = application.ApplicantId;
            task.Status = MarketplaceTaskStatus.InProgress;

            // Reject the remaining pending applications for this task.
            foreach (var other in task.Applications.Where(a => a.Id != applicationId && a.Status == ApplicationStatus.Pending))
                other.Status = ApplicationStatus.Rejected;

            _logger.LogInformation("Application accepted. TaskId: {TaskId}, AssigneeId: {AssigneeId}",
                task.Id, application.ApplicantId);
        }
        else
        {
            application.Status = ApplicationStatus.Rejected;
            _logger.LogInformation("Application rejected. ApplicationId: {ApplicationId}", applicationId);
        }

        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    // --- mapping ---

    private static TaskDetailDto MapDetail(MarketplaceTask t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        Budget = t.Budget,
        RequiredSkills = t.RequiredSkills,
        Deadline = t.Deadline,
        Status = t.Status.ToString(),
        PosterId = t.PosterId,
        PosterName = t.Poster?.Name ?? string.Empty,
        AssigneeId = t.AssigneeId,
        AssigneeName = t.Assignee?.Name,
        CreatedAt = t.CreatedAt,
        Applications = t.Applications
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => MapApplication(a, a.Applicant?.Name ?? string.Empty))
            .ToList(),
    };

    private static ApplicationDto MapApplication(TaskApplication a, string applicantName) => new()
    {
        Id = a.Id,
        TaskId = a.TaskId,
        ApplicantId = a.ApplicantId,
        ApplicantName = applicantName,
        CoverLetter = a.CoverLetter,
        ProposedRate = a.ProposedRate,
        Status = a.Status.ToString(),
        CreatedAt = a.CreatedAt,
    };
}
