using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Processes overdue tasks: notifies the owner and emits the "task.overdue" webhook,
/// exactly once per task (tracked via <see cref="ProjectTask.OverdueNotifiedAt"/>).
/// </summary>
public class OverdueService : IOverdueService
{
    // How long a task must stay overdue before it is escalated to the whole team.
    private static readonly TimeSpan EscalationThreshold = TimeSpan.FromDays(3);

    private readonly TaskpilotDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IWebhookService _webhooks;
    private readonly ILogger<OverdueService> _logger;

    public OverdueService(
        TaskpilotDbContext context,
        INotificationService notifications,
        IWebhookService webhooks,
        ILogger<OverdueService> logger)
    {
        _context = context;
        _notifications = notifications;
        _webhooks = webhooks;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ProcessOverdueAsync()
    {
        var now = DateTime.UtcNow;

        // Tasks past their deadline, not Done, and not yet flagged.
        var tasks = await _context.ProjectTasks
            .Include(t => t.Project)
            .Where(t => t.Deadline != null
                        && t.Deadline < now
                        && t.Status != ProjectTaskStatus.Done
                        && t.OverdueNotifiedAt == null)
            .ToListAsync();

        foreach (var task in tasks)
        {
            task.OverdueNotifiedAt = now;

            // Notify the project owner.
            await _notifications.CreateAsync(
                task.Project.OwnerId,
                NotificationType.Task,
                $"Task overdue: \"{task.Title}\"",
                $"/projects/{task.ProjectId}");

            // Emit the webhook event.
            await _webhooks.DispatchAsync(WebhookEvents.TaskOverdue, new
            {
                taskId = task.Id,
                title = task.Title,
                projectId = task.ProjectId,
                deadline = task.Deadline,
            });
        }

        // Escalate tasks that have stayed overdue past the threshold (runs
        // independently of whether any task was newly flagged above).
        await EscalateAsync(now);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Overdue check processed {Count} newly-overdue task(s).", tasks.Count);
        return tasks.Count;
    }

    /// <summary>
    /// Escalates tasks still overdue past <see cref="EscalationThreshold"/>: notifies the
    /// project owner and every member, and emits the "escalation.triggered" webhook, once
    /// per task (tracked via <see cref="ProjectTask.EscalatedAt"/>).
    /// </summary>
    private async Task EscalateAsync(DateTime now)
    {
        var cutoff = now - EscalationThreshold;

        // Overdue beyond the threshold, not Done, and not yet escalated.
        var tasks = await _context.ProjectTasks
            .Include(t => t.Project).ThenInclude(p => p.Members)
            .Where(t => t.Deadline != null
                        && t.Deadline < cutoff
                        && t.Status != ProjectTaskStatus.Done
                        && t.EscalatedAt == null)
            .ToListAsync();

        foreach (var task in tasks)
        {
            task.EscalatedAt = now;

            // The owner plus every collaborator on the project (distinct).
            var recipients = task.Project.Members
                .Select(m => m.UserId)
                .Append(task.Project.OwnerId)
                .Distinct();

            foreach (var recipientId in recipients)
                await _notifications.CreateAsync(
                    recipientId,
                    NotificationType.Task,
                    $"Escalation: \"{task.Title}\" is still overdue",
                    $"/projects/{task.ProjectId}");

            // Emit the escalation webhook so external tools can react.
            await _webhooks.DispatchAsync(WebhookEvents.EscalationTriggered, new
            {
                taskId = task.Id,
                title = task.Title,
                projectId = task.ProjectId,
                deadline = task.Deadline,
                escalatedAt = now,
            });

            _logger.LogWarning("Task escalated (overdue > {Days}d). TaskId: {TaskId}",
                EscalationThreshold.TotalDays, task.Id);
        }
    }
}
