using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// Read-only tools over the signed-in user's data. All queries are scoped by userId, so
/// the assistant can never see another user's tasks even if the model asks it to.
/// </summary>
public class AssistantToolbox : IAssistantToolbox
{
    private const int MaxRows = 25;

    private readonly TaskpilotDbContext _context;

    public AssistantToolbox(TaskpilotDbContext context)
    {
        _context = context;
    }

    // Schemas kept tiny — the model only needs to know each tool's shape.
    private static readonly object NoParams = new { type = "object", properties = new { } };

    /// <inheritdoc />
    public IReadOnlyList<ToolDefinition> Definitions { get; } = new List<ToolDefinition>
    {
        new("get_overdue_tasks",
            "Returns the tasks assigned to the user that are overdue (past their deadline and not done).",
            NoParams),
        new("get_upcoming_deadlines",
            "Returns the user's tasks due within the next N days (default 7).",
            new
            {
                type = "object",
                properties = new { days = new { type = "integer", description = "How many days ahead to look (default 7)." } },
            }),
        new("get_my_tasks",
            "Returns tasks assigned to the user, optionally filtered by status.",
            new
            {
                type = "object",
                properties = new
                {
                    status = new
                    {
                        type = "string",
                        description = "Optional Kanban status filter.",
                        @enum = new[] { "Backlog", "InProgress", "Review", "Done" },
                    },
                },
            }),
        new("list_my_projects",
            "Lists the projects the user owns or is a member of, with task counts.",
            NoParams),
    };

    /// <inheritdoc />
    public Task<string> ExecuteAsync(Guid userId, string toolName, string argumentsJson) => toolName switch
    {
        "get_overdue_tasks" => GetOverdueTasksAsync(userId),
        "get_upcoming_deadlines" => GetUpcomingDeadlinesAsync(userId, ReadInt(argumentsJson, "days") ?? 7),
        "get_my_tasks" => GetMyTasksAsync(userId, ReadString(argumentsJson, "status")),
        "list_my_projects" => ListMyProjectsAsync(userId),
        _ => Task.FromResult(Json(new { error = $"Unknown tool: {toolName}" })),
    };

    private async Task<string> GetOverdueTasksAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var rows = await _context.ProjectTasks
            .Where(t => t.AssigneeId == userId && t.Status != ProjectTaskStatus.Done
                        && t.Deadline != null && t.Deadline < now)
            .OrderBy(t => t.Deadline)
            .Take(MaxRows)
            .Select(t => new { title = t.Title, project = t.Project.Name, deadline = t.Deadline, status = t.Status.ToString() })
            .AsNoTracking()
            .ToListAsync();

        return Json(new { count = rows.Count, tasks = rows });
    }

    private async Task<string> GetUpcomingDeadlinesAsync(Guid userId, int days)
    {
        if (days is < 1 or > 365) days = 7;
        var now = DateTime.UtcNow;
        var until = now.AddDays(days);
        var rows = await _context.ProjectTasks
            .Where(t => t.AssigneeId == userId && t.Status != ProjectTaskStatus.Done
                        && t.Deadline != null && t.Deadline >= now && t.Deadline <= until)
            .OrderBy(t => t.Deadline)
            .Take(MaxRows)
            .Select(t => new { title = t.Title, project = t.Project.Name, deadline = t.Deadline, status = t.Status.ToString() })
            .AsNoTracking()
            .ToListAsync();

        return Json(new { days, count = rows.Count, tasks = rows });
    }

    private async Task<string> GetMyTasksAsync(Guid userId, string? status)
    {
        var query = _context.ProjectTasks.Where(t => t.AssigneeId == userId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProjectTaskStatus>(status, ignoreCase: true, out var s))
            query = query.Where(t => t.Status == s);

        var rows = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(MaxRows)
            .Select(t => new { title = t.Title, project = t.Project.Name, status = t.Status.ToString(), deadline = t.Deadline })
            .AsNoTracking()
            .ToListAsync();

        return Json(new { count = rows.Count, tasks = rows });
    }

    private async Task<string> ListMyProjectsAsync(Guid userId)
    {
        var rows = await _context.Projects
            .Where(p => p.ArchivedAt == null && (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)))
            .Select(p => new
            {
                name = p.Name,
                owner = p.OwnerId == userId,
                tasks = p.Tasks.Count,
                done = p.Tasks.Count(t => t.Status == ProjectTaskStatus.Done),
            })
            .AsNoTracking()
            .ToListAsync();

        return Json(new { count = rows.Count, projects = rows });
    }

    // --- helpers ---

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private static int? ReadInt(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            // The model may omit arguments entirely (JSON null) or send a non-object; treat as "no value".
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty(prop, out var v))
                return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
            // Some models pass numbers as strings, e.g. {"days":"7"}.
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var m)) return m;
            return null;
        }
        catch (JsonException) { return null; }
    }

    private static string? ReadString(string json, string prop)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty(prop, out var v))
                return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
