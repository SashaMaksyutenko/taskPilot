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

        if (tasks.Count == 0)
            return 0;

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

        await _context.SaveChangesAsync();
        _logger.LogInformation("Overdue check processed {Count} task(s).", tasks.Count);
        return tasks.Count;
    }
}
