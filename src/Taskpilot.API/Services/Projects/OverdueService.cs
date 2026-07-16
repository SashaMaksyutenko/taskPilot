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
    // Escalation tiers by days overdue: level 1 = team (3d), 2 = critical (5d), 3 = admin (7d).
    private static readonly int[] TierDays = { 3, 5, 7 };
    private const int MaxLevel = 3;

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
    /// Escalates tasks that stay overdue, through tiers by days overdue: team (3d),
    /// critical (5d) and admin (7d). Each tier fires exactly once (tracked via
    /// <see cref="ProjectTask.EscalationLevel"/>), notifying the tier's audience and
    /// emitting the "escalation.triggered" webhook with the level reached.
    /// </summary>
    private async Task EscalateAsync(DateTime now)
    {
        var firstCutoff = now - TimeSpan.FromDays(TierDays[0]);

        // Overdue past the first tier, not Done, and not yet at the top tier.
        var tasks = await _context.ProjectTasks
            .Include(t => t.Project).ThenInclude(p => p.Members)
            .Where(t => t.Deadline != null
                        && t.Deadline < firstCutoff
                        && t.Status != ProjectTaskStatus.Done
                        && t.EscalationLevel < MaxLevel)
            .ToListAsync();

        if (tasks.Count == 0)
            return;

        // Admins receive the top-tier (7d) escalation.
        var adminIds = await _context.Users
            .Where(u => u.Role == Role.Admin && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var task in tasks)
        {
            var daysOverdue = (now - task.Deadline!.Value).TotalDays;
            var target = TargetLevel(daysOverdue);

            // Fire each newly-reached tier in order.
            for (var level = task.EscalationLevel + 1; level <= target; level++)
                await FireLevelAsync(task, level, now, adminIds);

            if (target > task.EscalationLevel)
            {
                task.EscalationLevel = target;
                task.EscalatedAt = now;
            }
        }
    }

    /// <summary>The highest escalation tier a task qualifies for by days overdue.</summary>
    private static int TargetLevel(double daysOverdue)
    {
        var level = 0;
        for (var i = 0; i < TierDays.Length; i++)
            if (daysOverdue >= TierDays[i]) level = i + 1;
        return level;
    }

    /// <summary>Notifies the tier's audience and emits the escalation webhook for one level.</summary>
    private async Task FireLevelAsync(ProjectTask task, int level, DateTime now, List<Guid> adminIds)
    {
        // Owner + members for levels 1–2; owner + admins for level 3 (admin tier).
        var recipients = level >= 3
            ? adminIds.Append(task.Project.OwnerId).Distinct()
            : task.Project.Members.Select(m => m.UserId).Append(task.Project.OwnerId).Distinct();

        var message = level switch
        {
            1 => $"Escalation: \"{task.Title}\" is {TierDays[0]}+ days overdue",
            2 => $"Critical: \"{task.Title}\" is {TierDays[1]}+ days overdue",
            _ => $"\"{task.Title}\" is {TierDays[2]}+ days overdue — admins notified",
        };

        foreach (var recipientId in recipients)
            await _notifications.CreateAsync(recipientId, NotificationType.Task, message, $"/projects/{task.ProjectId}");

        await _webhooks.DispatchAsync(WebhookEvents.EscalationTriggered, new
        {
            taskId = task.Id,
            title = task.Title,
            projectId = task.ProjectId,
            deadline = task.Deadline,
            level,
            escalatedAt = now,
        });

        _logger.LogWarning("Task escalated to level {Level}. TaskId: {TaskId}", level, task.Id);
    }
}
