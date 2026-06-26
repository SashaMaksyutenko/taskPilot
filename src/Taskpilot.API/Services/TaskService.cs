using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Calendar;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Handles project-task business logic. A task is reachable only through a project
/// the user owns, so every operation checks that ownership first.
/// </summary>
public class TaskService : ITaskService
{
    private readonly TaskpilotDbContext _context;
    private readonly IWebhookService _webhooks;
    private readonly ILogger<TaskService> _logger;

    public TaskService(TaskpilotDbContext context, IWebhookService webhooks, ILogger<TaskService> logger)
    {
        _context = context;
        _webhooks = webhooks;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> CreateTaskAsync(Guid userId, Guid projectId, CreateTaskDto dto)
    {
        var ownsProject = await _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        if (!ownsProject)
            return Result<TaskDto>.Fail("Project not found.");

        // Priority defaults to Medium.
        var priority = TaskPriority.Medium;
        if (!string.IsNullOrWhiteSpace(dto.Priority) &&
            !Enum.TryParse(dto.Priority, ignoreCase: true, out priority))
            return Result<TaskDto>.Fail("Invalid priority.");

        // Optional assignee must exist.
        if (dto.AssigneeId.HasValue &&
            !await _context.Users.AnyAsync(u => u.Id == dto.AssigneeId.Value))
            return Result<TaskDto>.Fail("Assignee not found.");

        // Optional parent task must belong to the same project.
        if (dto.ParentTaskId.HasValue &&
            !await _context.ProjectTasks.AnyAsync(t => t.Id == dto.ParentTaskId.Value && t.ProjectId == projectId))
            return Result<TaskDto>.Fail("Parent task not found in this project.");

        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            Status = ProjectTaskStatus.Backlog,
            Priority = priority,
            AssigneeId = dto.AssigneeId,
            CreatorId = userId,
            ParentTaskId = dto.ParentTaskId,
            Deadline = dto.Deadline,
            CreatedAt = DateTime.UtcNow,
        };
        _context.ProjectTasks.Add(task);
        await _context.SaveChangesAsync();

        await _webhooks.DispatchAsync(WebhookEvents.TaskCreated, new
        {
            taskId = task.Id,
            title = task.Title,
            projectId,
            priority = task.Priority.ToString(),
        });

        _logger.LogInformation("Task created. TaskId: {TaskId}, ProjectId: {ProjectId}", task.Id, projectId);
        return Result<TaskDto>.Ok(await LoadDtoAsync(task.Id));
    }

    /// <inheritdoc />
    public async Task<Result<List<TaskDto>>> GetTasksAsync(Guid userId, Guid projectId, string? status)
    {
        var ownsProject = await _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        if (!ownsProject)
            return Result<List<TaskDto>>.Fail("Project not found.");

        ProjectTaskStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ProjectTaskStatus>(status, ignoreCase: true, out var parsed))
                return Result<List<TaskDto>>.Fail("Invalid status.");
            statusFilter = parsed;
        }

        var tasks = await _context.ProjectTasks
            .Where(t => t.ProjectId == projectId && (statusFilter == null || t.Status == statusFilter))
            .Include(t => t.Assignee)
            .Include(t => t.Creator)
            .OrderBy(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<TaskDto>>.Ok(tasks.Select(MapDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> GetTaskAsync(Guid userId, Guid taskId)
    {
        var task = await LoadOwnedAsync(taskId, userId);
        return task is null
            ? Result<TaskDto>.Fail("Task not found.")
            : Result<TaskDto>.Ok(MapDto(task));
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> UpdateTaskAsync(Guid userId, Guid taskId, UpdateTaskDto dto)
    {
        var task = await LoadOwnedAsync(taskId, userId);
        if (task is null)
            return Result<TaskDto>.Fail("Task not found.");

        var priority = task.Priority;
        if (!string.IsNullOrWhiteSpace(dto.Priority) &&
            !Enum.TryParse(dto.Priority, ignoreCase: true, out priority))
            return Result<TaskDto>.Fail("Invalid priority.");

        if (dto.AssigneeId.HasValue &&
            !await _context.Users.AnyAsync(u => u.Id == dto.AssigneeId.Value))
            return Result<TaskDto>.Fail("Assignee not found.");

        task.Title = dto.Title.Trim();
        task.Description = dto.Description?.Trim();
        task.Priority = priority;
        task.AssigneeId = dto.AssigneeId;
        task.Deadline = dto.Deadline;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _webhooks.DispatchAsync(WebhookEvents.TaskUpdated, new
        {
            taskId = task.Id,
            title = task.Title,
            projectId = task.ProjectId,
            priority = task.Priority.ToString(),
            assigneeId = task.AssigneeId,
            deadline = task.Deadline,
            updatedAt = task.UpdatedAt,
        });

        return Result<TaskDto>.Ok(await LoadDtoAsync(task.Id));
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> ChangeStatusAsync(Guid userId, Guid taskId, string status)
    {
        if (!Enum.TryParse<ProjectTaskStatus>(status, ignoreCase: true, out var parsed))
            return Result<TaskDto>.Fail("Invalid status.");

        var task = await LoadOwnedAsync(taskId, userId);
        if (task is null)
            return Result<TaskDto>.Fail("Task not found.");

        task.Status = parsed;
        // Track completion time when moving to/away from Done.
        task.CompletedAt = parsed == ProjectTaskStatus.Done ? DateTime.UtcNow : null;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Emit the task.completed webhook event when a task is finished.
        if (parsed == ProjectTaskStatus.Done)
        {
            await _webhooks.DispatchAsync(WebhookEvents.TaskCompleted, new
            {
                taskId = task.Id,
                title = task.Title,
                projectId = task.ProjectId,
                completedAt = task.CompletedAt,
            });
        }

        _logger.LogInformation("Task status changed. TaskId: {TaskId}, Status: {Status}", taskId, parsed);
        return Result<TaskDto>.Ok(await LoadDtoAsync(task.Id));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteTaskAsync(Guid userId, Guid taskId)
    {
        var task = await LoadOwnedAsync(taskId, userId);
        if (task is null)
            return Result.Fail("Task not found.");

        _context.ProjectTasks.Remove(task);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result<List<CalendarTaskDto>>> GetOverdueTasksAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        var tasks = await _context.ProjectTasks
            // Overdue = has a past deadline and is not yet Done.
            .Where(t => t.Project.OwnerId == userId
                        && t.Deadline != null
                        && t.Deadline < now
                        && t.Status != ProjectTaskStatus.Done)
            .Include(t => t.Project)
            .OrderBy(t => t.Deadline)
            .AsNoTracking()
            .ToListAsync();

        var items = tasks.Select(t => new CalendarTaskDto
        {
            Id = t.Id,
            Title = t.Title,
            ProjectId = t.ProjectId,
            ProjectName = t.Project.Name,
            Status = t.Status.ToString(),
            Priority = t.Priority.ToString(),
            Deadline = t.Deadline!.Value,
        }).ToList();

        return Result<List<CalendarTaskDto>>.Ok(items);
    }

    /// <inheritdoc />
    public async Task<Result<List<CalendarTaskDto>>> GetCalendarTasksAsync(Guid userId, DateTime from, DateTime to)
    {
        var tasks = await _context.ProjectTasks
            .Where(t => t.Project.OwnerId == userId
                        && t.Deadline != null
                        && t.Deadline >= from
                        && t.Deadline <= to)
            .Include(t => t.Project)
            .OrderBy(t => t.Deadline)
            .AsNoTracking()
            .ToListAsync();

        var items = tasks.Select(t => new CalendarTaskDto
        {
            Id = t.Id,
            Title = t.Title,
            ProjectId = t.ProjectId,
            ProjectName = t.Project.Name,
            Status = t.Status.ToString(),
            Priority = t.Priority.ToString(),
            Deadline = t.Deadline!.Value,
        }).ToList();

        return Result<List<CalendarTaskDto>>.Ok(items);
    }

    /// <inheritdoc />
    public async Task<Result<string>> ExportTasksCsvAsync(Guid userId, Guid projectId)
    {
        var ownsProject = await _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        if (!ownsProject)
            return Result<string>.Fail("Project not found.");

        var tasks = await _context.ProjectTasks
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.Assignee)
            .OrderBy(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Title,Status,Priority,Assignee,Deadline,CreatedAt,CompletedAt");
        foreach (var t in tasks)
        {
            sb.AppendLine(string.Join(",",
                Csv(t.Title),
                Csv(t.Status.ToString()),
                Csv(t.Priority.ToString()),
                Csv(t.Assignee?.Name ?? string.Empty),
                Csv(t.Deadline?.ToString("u") ?? string.Empty),
                Csv(t.CreatedAt.ToString("u")),
                Csv(t.CompletedAt?.ToString("u") ?? string.Empty)));
        }

        return Result<string>.Ok(sb.ToString());
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportTasksXlsxAsync(Guid userId, Guid projectId)
    {
        var ownsProject = await _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        if (!ownsProject)
            return Result<byte[]>.Fail("Project not found.");

        var tasks = await _context.ProjectTasks
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.Assignee)
            .OrderBy(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Tasks");

        // Header row.
        string[] headers = { "Title", "Status", "Priority", "Assignee", "Deadline", "Created", "Completed" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        // Data rows.
        var row = 2;
        foreach (var t in tasks)
        {
            ws.Cell(row, 1).Value = t.Title;
            ws.Cell(row, 2).Value = t.Status.ToString();
            ws.Cell(row, 3).Value = t.Priority.ToString();
            ws.Cell(row, 4).Value = t.Assignee?.Name ?? string.Empty;
            ws.Cell(row, 5).Value = t.Deadline?.ToString("u") ?? string.Empty;
            ws.Cell(row, 6).Value = t.CreatedAt.ToString("u");
            ws.Cell(row, 7).Value = t.CompletedAt?.ToString("u") ?? string.Empty;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Result<byte[]>.Ok(stream.ToArray());
    }

    /// <inheritdoc />
    public async Task<Result<byte[]>> ExportTasksPdfAsync(Guid userId, Guid projectId)
    {
        var project = await _context.Projects
            .Where(p => p.Id == projectId && p.OwnerId == userId)
            .Select(p => new { p.Name })
            .FirstOrDefaultAsync();
        if (project is null)
            return Result<byte[]>.Fail("Project not found.");

        var tasks = await _context.ProjectTasks
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.Assignee)
            .OrderBy(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text($"Tasks — {project.Name}").FontSize(18).Bold();

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3); // Title
                        columns.RelativeColumn();  // Status
                        columns.RelativeColumn();  // Priority
                        columns.RelativeColumn(2); // Assignee
                        columns.RelativeColumn(2); // Deadline
                    });

                    table.Header(header =>
                    {
                        foreach (var h in new[] { "Title", "Status", "Priority", "Assignee", "Deadline" })
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(h).Bold();
                    });

                    foreach (var task in tasks)
                    {
                        table.Cell().Padding(4).Text(task.Title);
                        table.Cell().Padding(4).Text(task.Status.ToString());
                        table.Cell().Padding(4).Text(task.Priority.ToString());
                        table.Cell().Padding(4).Text(task.Assignee?.Name ?? string.Empty);
                        table.Cell().Padding(4).Text(task.Deadline?.ToString("u") ?? string.Empty);
                    }
                });

                page.Footer().AlignRight().Text($"Generated {DateTime.UtcNow:u}").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();

        return Result<byte[]>.Ok(pdf);
    }

    // --- helpers ---

    /// <summary>
    /// Escapes a value for CSV: fields containing a comma, quote or newline are wrapped
    /// in double quotes, and inner quotes are doubled.
    /// </summary>
    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    /// <summary>Loads a task (with assignee/creator) only if the caller owns its project.</summary>
    private async Task<ProjectTask?> LoadOwnedAsync(Guid taskId, Guid userId)
    {
        var task = await _context.ProjectTasks
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.Creator)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        return task is not null && task.Project.OwnerId == userId ? task : null;
    }

    /// <summary>Reloads a task as a DTO (with assignee/creator names).</summary>
    private async Task<TaskDto> LoadDtoAsync(Guid taskId)
    {
        var task = await _context.ProjectTasks
            .Include(t => t.Assignee)
            .Include(t => t.Creator)
            .AsNoTracking()
            .FirstAsync(t => t.Id == taskId);
        return MapDto(task);
    }

    private static TaskDto MapDto(ProjectTask t) => new()
    {
        Id = t.Id,
        ProjectId = t.ProjectId,
        Title = t.Title,
        Description = t.Description,
        Status = t.Status.ToString(),
        Priority = t.Priority.ToString(),
        AssigneeId = t.AssigneeId,
        AssigneeName = t.Assignee?.Name,
        CreatorId = t.CreatorId,
        CreatorName = t.Creator?.Name ?? string.Empty,
        ParentTaskId = t.ParentTaskId,
        Deadline = t.Deadline,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        CompletedAt = t.CompletedAt,
    };
}
