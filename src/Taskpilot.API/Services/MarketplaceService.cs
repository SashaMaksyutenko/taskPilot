using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Common;
using Taskpilot.API.DTOs.Marketplace;
using Taskpilot.API.Mappers;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles marketplace business logic: posting tasks, browsing them, applying,
/// and accepting/rejecting applications.
/// </summary>
public class MarketplaceService : IMarketplaceService
{
    private readonly TaskpilotDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IWebhookService _webhooks;
    private readonly IPaymentClient _payments;
    private readonly IAuditService _audit;
    private readonly ILogger<MarketplaceService> _logger;

    public MarketplaceService(
        TaskpilotDbContext context,
        INotificationService notifications,
        IWebhookService webhooks,
        IPaymentClient payments,
        IAuditService audit,
        ILogger<MarketplaceService> logger)
    {
        _context = context;
        _notifications = notifications;
        _webhooks = webhooks;
        _payments = payments;
        _audit = audit;
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
    public async Task<Result<PagedResult<TaskListItemDto>>> GetTasksAsync(int page = 1, int pageSize = 20)
    {
        // Clamp paging to sane bounds.
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var total = await _context.MarketplaceTasks.CountAsync();

        var rows = await _context.MarketplaceTasks
            .OrderBy(t => t.Status)          // Open (0) first
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Budget,
                t.RequiredSkills,
                t.Deadline,
                t.Status,
                t.PaymentStatus,
                t.PosterId,
                PosterName = t.Poster.Name,
                PosterAvatarFileId = t.Poster.AvatarFileId,
                ApplicationCount = t.Applications.Count,
                t.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        // Compose the avatar URL in memory (EF can't translate the interpolation).
        var tasks = rows
            .Select(t => new TaskListItemDto
            {
                Id = t.Id,
                Title = t.Title,
                Budget = t.Budget,
                RequiredSkills = t.RequiredSkills,
                Deadline = t.Deadline,
                Status = t.Status.ToString(),
                PaymentStatus = t.PaymentStatus.ToString(),
                PosterId = t.PosterId,
                PosterName = t.PosterName,
                PosterAvatarUrl = UserMapper.AvatarUrl(t.PosterId, t.PosterAvatarFileId),
                ApplicationCount = t.ApplicationCount,
                CreatedAt = t.CreatedAt,
            })
            .ToList();

        return Result<PagedResult<TaskListItemDto>>.Ok(new PagedResult<TaskListItemDto>
        {
            Items = tasks,
            Total = total,
            Page = page,
            PageSize = pageSize,
        });
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

            var applicant = await _context.Users
                .Where(u => u.Id == applicantId)
                .Select(u => new { u.Name, u.AvatarFileId })
                .FirstAsync();
            var applicantName = applicant.Name;

            // Notify the task poster about the new application.
            await _notifications.CreateAsync(
                task.PosterId,
                NotificationType.Marketplace,
                $"{applicantName} applied to your task \"{task.Title}\".",
                $"/marketplace/{task.Id}");

            _logger.LogInformation("Application submitted. ApplicationId: {ApplicationId}", application.Id);
            return Result<ApplicationDto>.Ok(MapApplication(application, applicantName,
                UserMapper.AvatarUrl(applicantId, applicant.AvatarFileId)));
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

        // Notify the applicant about the decision.
        await _notifications.CreateAsync(
            application.ApplicantId,
            NotificationType.Marketplace,
            accept
                ? $"Your application to \"{task.Title}\" was accepted!"
                : $"Your application to \"{task.Title}\" was rejected.",
            $"/marketplace/{task.Id}");

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> SubmitTaskAsync(Guid assigneeId, Guid taskId)
    {
        var task = await _context.MarketplaceTasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return Result.Fail("Task not found.");

        if (task.AssigneeId != assigneeId)
            return Result.Fail("Only the assignee can submit this task.");

        if (task.Status != MarketplaceTaskStatus.InProgress)
            return Result.Fail("The task is not in progress.");

        task.Status = MarketplaceTaskStatus.Submitted;
        await _context.SaveChangesAsync();

        // Tell the poster the work is ready for review.
        await _notifications.CreateAsync(
            task.PosterId,
            NotificationType.Marketplace,
            $"Work submitted for \"{task.Title}\" — ready for review.",
            $"/marketplace/{task.Id}");

        _logger.LogInformation("Marketplace task submitted. TaskId: {TaskId}", taskId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> ApproveTaskAsync(Guid posterId, Guid taskId)
    {
        var task = await _context.MarketplaceTasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return Result.Fail("Task not found.");

        if (task.PosterId != posterId)
            return Result.Fail("Only the poster can approve this task.");

        if (task.Status != MarketplaceTaskStatus.Submitted)
            return Result.Fail("The task has not been submitted yet.");

        task.Status = MarketplaceTaskStatus.Completed;
        await _context.SaveChangesAsync();

        // Tell the assignee their work was approved.
        if (task.AssigneeId is { } assigneeId)
            await _notifications.CreateAsync(
                assigneeId,
                NotificationType.Marketplace,
                $"Your work on \"{task.Title}\" was approved!",
                $"/marketplace/{task.Id}");

        await _webhooks.DispatchAsync(WebhookEvents.MarketplaceTaskCompleted, new
        {
            taskId = task.Id,
            title = task.Title,
            posterId = task.PosterId,
            assigneeId = task.AssigneeId,
        });

        _logger.LogInformation("Marketplace task approved/completed. TaskId: {TaskId}", taskId);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<string>> CreatePaymentAsync(Guid posterId, Guid taskId, string successUrl, string cancelUrl)
    {
        if (!_payments.IsEnabled)
            return Result<string>.Fail("Payments are not configured.");

        var task = await _context.MarketplaceTasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return Result<string>.Fail("Task not found.");

        if (task.PosterId != posterId)
            return Result<string>.Fail("Only the poster can pay for this task.");

        if (task.Status != MarketplaceTaskStatus.Completed)
            return Result<string>.Fail("Only a completed task can be paid.");

        if (task.PaymentStatus == PaymentStatus.Paid)
            return Result<string>.Fail("This task has already been paid.");

        var session = await _payments.CreateCheckoutSessionAsync(
            task.Budget, $"TaskPilot: {task.Title}", successUrl, cancelUrl);
        if (!session.Succeeded || session.Value is null)
            return Result<string>.Fail(session.Error ?? "Could not start the payment.");

        task.PaymentSessionId = session.Value.Id;
        task.PaymentStatus = PaymentStatus.Pending;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment session created. TaskId: {TaskId}", taskId);
        return Result<string>.Ok(session.Value.Url);
    }

    /// <inheritdoc />
    public async Task<Result> ConfirmPaymentAsync(Guid posterId, Guid taskId, string? ip = null)
    {
        var task = await _context.MarketplaceTasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return Result.Fail("Task not found.");

        if (task.PosterId != posterId)
            return Result.Fail("Only the poster can confirm this payment.");

        if (task.PaymentStatus == PaymentStatus.Paid)
            return Result.Ok();  // Idempotent: already confirmed.

        if (string.IsNullOrEmpty(task.PaymentSessionId))
            return Result.Fail("No payment is in progress for this task.");

        var paid = await _payments.IsSessionPaidAsync(task.PaymentSessionId);
        if (!paid.Succeeded)
            return Result.Fail(paid.Error ?? "Could not verify the payment.");
        if (!paid.Value)
            return Result.Fail("The payment has not been completed.");

        var posterEmail = await _context.Users
            .Where(u => u.Id == posterId).Select(u => u.Email).FirstOrDefaultAsync();
        await MarkTaskPaidAsync(task, posterId, posterEmail, ip);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> ConfirmPaymentBySessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return Result.Fail("Session id is required.");

        var task = await _context.MarketplaceTasks.FirstOrDefaultAsync(t => t.PaymentSessionId == sessionId);
        if (task is null)
            return Result.Fail("No task matches this payment session.");

        if (task.PaymentStatus == PaymentStatus.Paid)
            return Result.Ok(); // Idempotent: webhook may be delivered more than once.

        // The signed Stripe event is authoritative that the session was paid.
        await MarkTaskPaidAsync(task, actorId: null, actorEmail: "stripe.webhook", ip: null);
        return Result.Ok();
    }

    /// <summary>Marks a completed task paid and fires the notification, audit and webhook (once).</summary>
    private async Task MarkTaskPaidAsync(MarketplaceTask task, Guid? actorId, string? actorEmail, string? ip)
    {
        task.PaymentStatus = PaymentStatus.Paid;
        task.PaidAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (task.AssigneeId is { } assigneeId)
            await _notifications.CreateAsync(
                assigneeId,
                NotificationType.Marketplace,
                $"You were paid {task.Budget:0.##} for \"{task.Title}\".",
                $"/marketplace/{task.Id}");

        await _audit.LogAsync(
            action: "marketplace.task.paid",
            actorId: actorId,
            actorEmail: actorEmail,
            entityType: nameof(MarketplaceTask),
            entityId: task.Id.ToString(),
            details: $"Paid {task.Budget:0.##} for \"{task.Title}\"",
            ipAddress: ip);

        await _webhooks.DispatchAsync(WebhookEvents.MarketplaceTaskPaid, new
        {
            taskId = task.Id,
            title = task.Title,
            amount = task.Budget,
            posterId = task.PosterId,
            assigneeId = task.AssigneeId,
        });

        _logger.LogInformation("Marketplace task paid. TaskId: {TaskId}", task.Id);
    }

    /// <inheritdoc />
    public async Task<Result> RateAsync(Guid raterId, Guid taskId, int stars, string? comment)
    {
        if (stars < 1 || stars > 5)
            return Result.Fail("Rating must be between 1 and 5 stars.");

        var task = await _context.MarketplaceTasks.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null)
            return Result.Fail("Task not found.");

        if (task.Status != MarketplaceTaskStatus.Completed)
            return Result.Fail("You can only rate a completed task.");

        // The rater must be a party of the task; the ratee is the other party.
        Guid rateeId;
        if (raterId == task.PosterId)
            rateeId = task.AssigneeId ?? Guid.Empty;
        else if (raterId == task.AssigneeId)
            rateeId = task.PosterId;
        else
            return Result.Fail("Only the poster or the assignee can rate this task.");

        if (rateeId == Guid.Empty)
            return Result.Fail("There is no counterpart to rate.");

        var alreadyRated = await _context.Reviews
            .AnyAsync(r => r.MarketplaceTaskId == taskId && r.RaterId == raterId);
        if (alreadyRated)
            return Result.Fail("You have already rated this task.");

        _context.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(),
            MarketplaceTaskId = taskId,
            RaterId = raterId,
            RateeId = rateeId,
            Stars = stars,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        await _notifications.CreateAsync(
            rateeId,
            NotificationType.Marketplace,
            $"You received a {stars}★ rating for \"{task.Title}\".",
            $"/marketplace/{taskId}");

        _logger.LogInformation("Marketplace review left. TaskId: {TaskId}, RaterId: {RaterId}, Stars: {Stars}", taskId, raterId, stars);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<List<ReviewDto>>> GetReviewsAsync(Guid taskId)
    {
        // Join to Users for the rater's name + avatar (rater/ratee are plain ids on Review).
        var rows = await _context.Reviews
            .Where(r => r.MarketplaceTaskId == taskId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.RaterId,
                Rater = _context.Users.Where(u => u.Id == r.RaterId).Select(u => new { u.Name, u.AvatarFileId }).FirstOrDefault(),
                r.RateeId,
                r.Stars,
                r.Comment,
                r.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync();

        var reviews = rows
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                RaterId = r.RaterId,
                RaterName = r.Rater?.Name ?? string.Empty,
                RaterAvatarUrl = UserMapper.AvatarUrl(r.RaterId, r.Rater?.AvatarFileId),
                RateeId = r.RateeId,
                Stars = r.Stars,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
            })
            .ToList();

        return Result<List<ReviewDto>>.Ok(reviews);
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
        PosterAvatarUrl = t.Poster is null ? null : UserMapper.AvatarUrl(t.Poster),
        AssigneeId = t.AssigneeId,
        AssigneeName = t.Assignee?.Name,
        AssigneeAvatarUrl = t.Assignee is null ? null : UserMapper.AvatarUrl(t.Assignee),
        PaymentStatus = t.PaymentStatus.ToString(),
        PaidAt = t.PaidAt,
        CreatedAt = t.CreatedAt,
        Applications = t.Applications
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => MapApplication(a, a.Applicant?.Name ?? string.Empty,
                a.Applicant is null ? null : UserMapper.AvatarUrl(a.Applicant)))
            .ToList(),
    };

    private static ApplicationDto MapApplication(TaskApplication a, string applicantName, string? applicantAvatarUrl = null) => new()
    {
        Id = a.Id,
        TaskId = a.TaskId,
        ApplicantId = a.ApplicantId,
        ApplicantName = applicantName,
        ApplicantAvatarUrl = applicantAvatarUrl,
        CoverLetter = a.CoverLetter,
        ProposedRate = a.ProposedRate,
        Status = a.Status.ToString(),
        CreatedAt = a.CreatedAt,
    };
}
