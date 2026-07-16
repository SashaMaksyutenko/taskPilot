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
            "Returns overdue tasks (past deadline, not done) across every project the user owns or is a member of, "
            + "including each task's assignee. Use this for questions about what is overdue on the team/board.",
            NoParams),
        new("get_upcoming_deadlines",
            "Returns tasks due within the next N days (default 7) across every project the user owns or is a member of, "
            + "including each task's assignee.",
            new
            {
                type = "object",
                properties = new { days = new { type = "integer", description = "How many days ahead to look (default 7)." } },
            }),
        new("get_my_tasks",
            "Returns tasks assigned to the user personally, optionally filtered by status.",
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
        new("list_marketplace_tasks",
            "Lists tasks posted on the public marketplace (freelance gigs) — budget, status, required "
            + "skills, poster and application count. Use for any question about the marketplace/market. "
            + "Optionally filter by status.",
            new
            {
                type = "object",
                properties = new
                {
                    status = new
                    {
                        type = "string",
                        description = "Optional lifecycle filter.",
                        @enum = new[] { "Open", "InProgress", "Submitted", "Completed", "Cancelled" },
                    },
                },
            }),
        new("search_taskpilot",
            "Searches the user's projects and tasks, plus public forum topics and users, for a keyword. "
            + "Use to find something by name.",
            new
            {
                type = "object",
                properties = new { query = new { type = "string", description = "The text to search for." } },
                required = new[] { "query" },
            }),
        new("get_forum_topics",
            "Lists the most recent public forum topics (title, author, reply count).",
            NoParams),
        new("get_notifications",
            "Returns the user's unread notifications.",
            NoParams),
        new("get_project_stats",
            "For one of the user's projects (found by name), returns totals, a status breakdown, "
            + "per-assignee workload and the overdue count.",
            new
            {
                type = "object",
                properties = new { project = new { type = "string", description = "The project name, or part of it." } },
                required = new[] { "project" },
            }),
        new("get_platform_stats",
            "Returns public, site-wide TaskPilot statistics: total registered users, active users, the "
            + "newest user, and total forum topics and posts. Use for questions about how many users or "
            + "topics the platform has overall.",
            NoParams),
    };

    /// <inheritdoc />
    public Task<string> ExecuteAsync(Guid userId, string toolName, string argumentsJson) => toolName switch
    {
        "get_overdue_tasks" => GetOverdueTasksAsync(userId),
        "get_upcoming_deadlines" => GetUpcomingDeadlinesAsync(userId, ReadInt(argumentsJson, "days") ?? 7),
        "get_my_tasks" => GetMyTasksAsync(userId, ReadString(argumentsJson, "status")),
        "list_my_projects" => ListMyProjectsAsync(userId),
        "list_marketplace_tasks" => ListMarketplaceTasksAsync(ReadString(argumentsJson, "status")),
        "search_taskpilot" => SearchAsync(userId, ReadString(argumentsJson, "query")),
        "get_forum_topics" => GetForumTopicsAsync(ReadInt(argumentsJson, "limit") ?? 5),
        "get_notifications" => GetNotificationsAsync(userId),
        "get_project_stats" => GetProjectStatsAsync(userId, ReadString(argumentsJson, "project")),
        "get_platform_stats" => GetPlatformStatsAsync(),
        _ => Task.FromResult(Json(new { error = $"Unknown tool: {toolName}" })),
    };

    private async Task<string> GetOverdueTasksAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        // Scope by project access (owner or member), matching the dashboard's "Overdue tasks" list —
        // so the assistant sees the same tasks the user does, including ones assigned to teammates.
        var rows = await _context.ProjectTasks
            .Where(t => (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId))
                        && t.Status != ProjectTaskStatus.Done
                        && t.Deadline != null && t.Deadline < now)
            .OrderBy(t => t.Deadline)
            .Take(MaxRows)
            .Select(t => new
            {
                title = t.Title,
                project = t.Project.Name,
                assignee = t.Assignee != null ? t.Assignee.Name : null,
                deadline = t.Deadline,
                status = t.Status.ToString(),
            })
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
            .Where(t => (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId))
                        && t.Status != ProjectTaskStatus.Done
                        && t.Deadline != null && t.Deadline >= now && t.Deadline <= until)
            .OrderBy(t => t.Deadline)
            .Take(MaxRows)
            .Select(t => new
            {
                title = t.Title,
                project = t.Project.Name,
                assignee = t.Assignee != null ? t.Assignee.Name : null,
                deadline = t.Deadline,
                status = t.Status.ToString(),
            })
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

    private async Task<string> ListMarketplaceTasksAsync(string? status)
    {
        // The marketplace is public — every user sees every gig, so no per-user scoping here.
        var query = _context.MarketplaceTasks.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<MarketplaceTaskStatus>(status, ignoreCase: true, out var s))
            query = query.Where(m => m.Status == s);

        var rows = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxRows)
            .Select(m => new
            {
                title = m.Title,
                budget = m.Budget,
                status = m.Status.ToString(),
                skills = m.RequiredSkills,
                poster = m.Poster.Name,
                applications = m.Applications.Count,
                deadline = m.Deadline,
            })
            .AsNoTracking()
            .ToListAsync();

        return Json(new { count = rows.Count, tasks = rows });
    }

    private async Task<string> SearchAsync(Guid userId, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Json(new { error = "Provide a search query." });
        var q = query.Trim().ToLower();

        var projects = await _context.Projects
            .Where(p => (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)) && p.Name.ToLower().Contains(q))
            .OrderBy(p => p.Name).Take(5)
            .Select(p => new { name = p.Name })
            .AsNoTracking().ToListAsync();

        var tasks = await _context.ProjectTasks
            .Where(t => (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId))
                        && t.Title.ToLower().Contains(q))
            .OrderByDescending(t => t.CreatedAt).Take(5)
            .Select(t => new { title = t.Title, project = t.Project.Name, status = t.Status.ToString() })
            .AsNoTracking().ToListAsync();

        // Forum topics and users are public in the app's search.
        var topics = await _context.ForumTopics
            .Where(t => t.Title.ToLower().Contains(q))
            .OrderByDescending(t => t.CreatedAt).Take(5)
            .Select(t => new { title = t.Title, author = t.Author.Name })
            .AsNoTracking().ToListAsync();

        var users = await _context.Users
            .Where(u => u.IsActive && u.Name.ToLower().Contains(q))
            .OrderBy(u => u.Name).Take(5)
            .Select(u => new { name = u.Name })
            .AsNoTracking().ToListAsync();

        return Json(new { projects, tasks, topics, users });
    }

    private async Task<string> GetForumTopicsAsync(int take)
    {
        if (take is < 1 or > MaxRows) take = 5;
        var rows = await _context.ForumTopics
            .OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.CreatedAt)
            .Take(take)
            .Select(t => new
            {
                title = t.Title,
                author = t.Author.Name,
                replies = t.Replies.Count,
                pinned = t.IsPinned,
                createdAt = t.CreatedAt,
            })
            .AsNoTracking().ToListAsync();

        return Json(new { count = rows.Count, topics = rows });
    }

    private async Task<string> GetNotificationsAsync(Guid userId)
    {
        var rows = await _context.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(MaxRows)
            .Select(n => new { message = n.Message, type = n.Type.ToString(), createdAt = n.CreatedAt })
            .AsNoTracking().ToListAsync();

        return Json(new { unread = rows.Count, notifications = rows });
    }

    private async Task<string> GetProjectStatsAsync(Guid userId, string? projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            return Json(new { error = "Provide a project name." });
        var q = projectName.Trim().ToLower();

        var project = await _context.Projects
            .Where(p => (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)) && p.Name.ToLower().Contains(q))
            .OrderBy(p => p.Name.Length) // prefer the closest (shortest) name match
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync();
        if (project is null)
            return Json(new { error = $"No project you can access matches '{projectName}'." });

        var now = DateTime.UtcNow;
        var tasks = await _context.ProjectTasks
            .Where(t => t.ProjectId == project.Id)
            .Select(t => new { t.Status, t.Deadline, assignee = t.Assignee != null ? t.Assignee.Name : null })
            .AsNoTracking().ToListAsync();

        var byStatus = tasks.GroupBy(t => t.Status.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var workload = tasks
            .GroupBy(t => t.assignee ?? "Unassigned")
            .Select(g => new { assignee = g.Key, tasks = g.Count() })
            .OrderByDescending(x => x.tasks).ToList();
        var overdue = tasks.Count(t => t.Deadline != null && t.Deadline < now && t.Status != ProjectTaskStatus.Done);

        return Json(new { project = project.Name, total = tasks.Count, overdue, byStatus, workload });
    }

    private async Task<string> GetPlatformStatsAsync()
    {
        // Public, site-wide numbers — the same set shown on the public stats footer.
        var totalUsers = await _context.Users.CountAsync();
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
        var newestUser = await _context.Users
            .OrderByDescending(u => u.CreatedAt).Select(u => u.Name).FirstOrDefaultAsync();
        var forumTopics = await _context.ForumTopics.CountAsync();
        var forumPosts = await _context.ForumReplies.CountAsync();

        return Json(new { totalUsers, activeUsers, newestUser, forumTopics, forumPosts });
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
