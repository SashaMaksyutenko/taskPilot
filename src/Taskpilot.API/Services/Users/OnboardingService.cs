using Microsoft.Extensions.Options;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Seeds a small starter project so a new account opens onto a working board instead of an
/// empty dashboard. The example tasks are spread across all four Kanban columns and carry
/// deadlines, so the board, the calendar and the dashboard counters all have something to
/// show from the very first login.
/// </summary>
public class OnboardingService : IOnboardingService
{
    private readonly TaskpilotDbContext _context;
    private readonly OnboardingOptions _options;
    private readonly ILogger<OnboardingService> _logger;

    public OnboardingService(
        TaskpilotDbContext context,
        IOptions<OnboardingOptions> options,
        ILogger<OnboardingService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CreateStarterProjectAsync(Guid userId)
    {
        if (!_options.CreateSampleProject)
            return;

        try
        {
            var now = DateTime.UtcNow;
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = "Welcome to TaskPilot",
                Description = "A sample project to explore the board, calendar and task details. Delete it whenever you like.",
                Color = "#4F46E5",
                OwnerId = userId,
                CreatedAt = now,
            };
            _context.Projects.Add(project);

            // One task per column so the board is never empty, with deadlines spread over the
            // next two weeks so the calendar and the "due soon" counters are populated too.
            // The completed one is dated in the past, as a real finished task would be.
            var tasks = new[]
            {
                Task_("Drag me to another column", ProjectTaskStatus.Backlog, TaskPriority.Medium, now.AddDays(5),
                    "Every card can be dragged between columns — on a phone too, with a long press."),
                Task_("Open me to see the details", ProjectTaskStatus.InProgress, TaskPriority.High, now.AddDays(2),
                    "Task details hold the description, assignee, deadline, subtasks, comments, time tracking and the full change history."),
                Task_("Check the Calendar and Team views", ProjectTaskStatus.Review, TaskPriority.Low, now.AddDays(9),
                    "Tasks with a deadline appear on the calendar, where they can be rescheduled by dragging. The board's Team tab shows who is busy with what."),
                Task_("Create your own project", ProjectTaskStatus.Done, TaskPriority.Medium, now.AddDays(-1),
                    "Projects can be shared with teammates, who then get their own roles, a shared board and a project chat."),
            };

            foreach (var task in tasks)
            {
                task.ProjectId = project.Id;
                _context.ProjectTasks.Add(task);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Starter project created. UserId: {UserId}, ProjectId: {ProjectId}", userId, project.Id);

            // Local helper: keeps the task list above readable.
            ProjectTask Task_(string title, ProjectTaskStatus status, TaskPriority priority, DateTime deadline, string description) => new()
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = description,
                Status = status,
                Priority = priority,
                CreatorId = userId,
                AssigneeId = userId,          // assigned, so the Team view and "my tasks" are populated
                Deadline = deadline,
                CreatedAt = now,
                CompletedAt = status == ProjectTaskStatus.Done ? now.AddDays(-1) : null,
                Tags = new List<string> { "sample" },
            };
        }
        catch (Exception ex)
        {
            // Registration already succeeded; a missing sample project is a cosmetic loss,
            // so it must never surface as a failed sign-up.
            _logger.LogError(ex, "Could not create the starter project. UserId: {UserId}", userId);
        }
    }
}
