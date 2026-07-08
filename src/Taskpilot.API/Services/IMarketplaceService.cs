using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Common;
using Taskpilot.API.DTOs.Marketplace;

namespace Taskpilot.API.Services;

/// <summary>
/// Business logic for the marketplace: posting tasks, applying, and deciding applications.
/// </summary>
public interface IMarketplaceService
{
    /// <summary>Posts a new task.</summary>
    Task<Result<TaskDetailDto>> CreateTaskAsync(Guid posterId, CreateTaskDto dto);

    /// <summary>Lists a page of tasks (open first, then newest).</summary>
    Task<Result<PagedResult<TaskListItemDto>>> GetTasksAsync(int page = 1, int pageSize = 20);

    /// <summary>Returns a task with its applications.</summary>
    Task<Result<TaskDetailDto>> GetTaskAsync(Guid taskId);

    /// <summary>Submits an application to a task (if open, not the poster, not a duplicate).</summary>
    Task<Result<ApplicationDto>> ApplyAsync(Guid applicantId, ApplyDto dto);

    /// <summary>
    /// Accepts or rejects an application (poster only). Accepting assigns the task,
    /// moves it to In Progress and rejects the other pending applications.
    /// </summary>
    Task<Result> DecideApplicationAsync(Guid posterId, Guid applicationId, bool accept);

    /// <summary>Assignee submits their finished work (In Progress → Submitted).</summary>
    Task<Result> SubmitTaskAsync(Guid assigneeId, Guid taskId);

    /// <summary>Poster approves the submitted work (Submitted → Completed).</summary>
    Task<Result> ApproveTaskAsync(Guid posterId, Guid taskId);

    /// <summary>
    /// Poster starts paying the budget for a completed task, returning the Stripe
    /// Checkout URL to redirect to. Requires payments to be configured.
    /// </summary>
    Task<Result<string>> CreatePaymentAsync(Guid posterId, Guid taskId, string successUrl, string cancelUrl);

    /// <summary>
    /// Confirms a task's payment after the poster returns from Stripe: verifies the
    /// session was paid, marks the task paid, notifies the assignee and audits it.
    /// </summary>
    Task<Result> ConfirmPaymentAsync(Guid posterId, Guid taskId, string? ip = null);

    /// <summary>
    /// Leaves a 1–5 star review for a completed task. The rater must be the poster or
    /// the assignee; the review is about the other party. One review per rater per task.
    /// </summary>
    Task<Result> RateAsync(Guid raterId, Guid taskId, int stars, string? comment);

    /// <summary>Returns the reviews left for a task.</summary>
    Task<Result<List<ReviewDto>>> GetReviewsAsync(Guid taskId);
}
