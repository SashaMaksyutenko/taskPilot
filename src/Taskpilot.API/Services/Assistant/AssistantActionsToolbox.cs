using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Marketplace;
using Taskpilot.API.Services;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// Write tools the assistant can run on the user's behalf when explicitly asked
/// (create a task, move a task between statuses, apply to a marketplace gig, start a
/// forum topic, create a project). Every action goes through the normal services, so their
/// permission and validation rules apply — the assistant cannot do anything the user could
/// not do themselves through the UI.
/// </summary>
public class AssistantActionsToolbox : IAssistantToolbox
{
    private readonly TaskpilotDbContext _context;
    private readonly ITaskService _tasks;
    private readonly IMarketplaceService _marketplace;
    private readonly IForumService _forum;
    private readonly IProjectService _projects;

    public AssistantActionsToolbox(
        TaskpilotDbContext context, ITaskService tasks, IMarketplaceService marketplace,
        IForumService forum, IProjectService projects)
    {
        _context = context;
        _tasks = tasks;
        _marketplace = marketplace;
        _forum = forum;
        _projects = projects;
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolDefinition> Definitions { get; } = new List<ToolDefinition>
    {
        new("create_task",
            "Creates a new task in one of the user's projects. Only call this when the user clearly asks "
            + "to create/add a task; never speculatively.",
            new
            {
                type = "object",
                properties = new
                {
                    project = new { type = "string", description = "Name of the project to add the task to." },
                    title = new { type = "string", description = "Task title." },
                    description = new { type = "string", description = "Optional details." },
                    priority = new { type = "string", description = "Optional priority.", @enum = new[] { "Low", "Medium", "High" } },
                    deadline = new { type = "string", description = "Optional deadline as an ISO date (yyyy-MM-dd)." },
                    assignee = new { type = "string", description = "Optional name of a project member to assign it to." },
                },
                required = new[] { "project", "title" },
            }),
        new("apply_to_marketplace_task",
            "Submits an application to an open marketplace gig on the user's behalf. Only call this when the "
            + "user clearly asks to apply.",
            new
            {
                type = "object",
                properties = new
                {
                    task = new { type = "string", description = "Title of the marketplace gig to apply to." },
                    coverLetter = new { type = "string", description = "Optional short message to the poster." },
                    proposedRate = new { type = "number", description = "Optional proposed rate; defaults to the gig's budget." },
                },
                required = new[] { "task" },
            }),
        new("change_task_status",
            "Moves one of the user's tasks to a different Kanban status (Backlog, InProgress, Review, Done). "
            + "Only call this when the user clearly asks to move/complete a task.",
            new
            {
                type = "object",
                properties = new
                {
                    task = new { type = "string", description = "Title (or part of it) of the task to move." },
                    status = new { type = "string", description = "Target status.", @enum = new[] { "Backlog", "InProgress", "Review", "Done" } },
                    project = new { type = "string", description = "Optional project name, to disambiguate when several tasks match." },
                },
                required = new[] { "task", "status" },
            }),
        new("create_forum_topic",
            "Creates a new forum topic authored by the user. Only call this when the user clearly asks to "
            + "post/start a forum topic.",
            new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string", description = "Topic title." },
                    body = new { type = "string", description = "Topic body (Markdown allowed)." },
                    tags = new { type = "array", items = new { type = "string" }, description = "Optional tags." },
                },
                required = new[] { "title", "body" },
            }),
        new("create_project",
            "Creates a new project owned by the user. Only call this when the user clearly asks to create a project.",
            new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "Project name." },
                    description = new { type = "string", description = "Optional description." },
                },
                required = new[] { "name" },
            }),
    };

    /// <inheritdoc />
    public Task<string> ExecuteAsync(Guid userId, string toolName, string argumentsJson) => toolName switch
    {
        "create_task" => CreateTaskAsync(userId, argumentsJson),
        "apply_to_marketplace_task" => ApplyToMarketplaceTaskAsync(userId, argumentsJson),
        "change_task_status" => ChangeTaskStatusAsync(userId, argumentsJson),
        "create_forum_topic" => CreateForumTopicAsync(userId, argumentsJson),
        "create_project" => CreateProjectAsync(userId, argumentsJson),
        _ => Task.FromResult(Json(new { error = $"Unknown tool: {toolName}" })),
    };

    private async Task<string> CreateTaskAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var projectName = Str(args, "project");
        var title = Str(args, "title");
        if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(title))
            return Json(new { error = "Both 'project' and 'title' are required." });

        var q = projectName.Trim().ToLower();
        var project = await _context.Projects
            .Where(p => (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)) && p.Name.ToLower().Contains(q))
            .OrderBy(p => p.Name.Length) // prefer the closest (shortest) name match
            .Select(p => new { p.Id, p.Name })
            .FirstOrDefaultAsync();
        if (project is null)
            return Json(new { error = $"No project you can access matches '{projectName}'." });

        // Optional assignee — must be a member (or the owner) of the resolved project.
        Guid? assigneeId = null;
        var assigneeName = Str(args, "assignee");
        if (!string.IsNullOrWhiteSpace(assigneeName))
        {
            var an = assigneeName.Trim().ToLower();
            assigneeId = await _context.ProjectMembers
                .Where(m => m.ProjectId == project.Id && m.User.Name.ToLower().Contains(an))
                .Select(m => (Guid?)m.UserId)
                .FirstOrDefaultAsync();
            assigneeId ??= await _context.Projects
                .Where(p => p.Id == project.Id && p.Owner.Name.ToLower().Contains(an))
                .Select(p => (Guid?)p.OwnerId)
                .FirstOrDefaultAsync();
            if (assigneeId is null)
                return Json(new { error = $"No member named '{assigneeName}' in project '{project.Name}'." });
        }

        var dto = new DTOs.Projects.CreateTaskDto
        {
            Title = title.Trim(),
            Description = Str(args, "description"),
            Priority = NormalizePriority(Str(args, "priority")),
            Deadline = DateOpt(args, "deadline"),
            AssigneeId = assigneeId,
        };

        var result = await _tasks.CreateTaskAsync(userId, project.Id, dto);
        if (!result.Succeeded)
            return Json(new { error = result.Error });

        var t = result.Value!;
        return Json(new
        {
            created = true,
            task = new { title = t.Title, project = project.Name, status = t.Status, priority = t.Priority, deadline = t.Deadline, assignee = t.AssigneeName },
        });
    }

    private async Task<string> ApplyToMarketplaceTaskAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var taskTitle = Str(args, "task");
        if (string.IsNullOrWhiteSpace(taskTitle))
            return Json(new { error = "'task' (the gig title) is required." });

        var q = taskTitle.Trim().ToLower();
        var gig = await _context.MarketplaceTasks
            .Where(m => m.Status == Models.MarketplaceTaskStatus.Open && m.Title.ToLower().Contains(q))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new { m.Id, m.Title, m.Budget })
            .FirstOrDefaultAsync();
        if (gig is null)
            return Json(new { error = $"No open marketplace gig matches '{taskTitle}'." });

        var dto = new ApplyDto
        {
            TaskId = gig.Id,
            CoverLetter = Str(args, "coverLetter") is { Length: > 0 } cl ? cl : "I'd like to work on this.",
            ProposedRate = Dec(args, "proposedRate") ?? gig.Budget,
        };

        var result = await _marketplace.ApplyAsync(userId, dto);
        if (!result.Succeeded)
            return Json(new { error = result.Error });

        return Json(new { applied = true, gig = gig.Title, proposedRate = dto.ProposedRate });
    }

    private async Task<string> ChangeTaskStatusAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var taskTitle = Str(args, "task");
        var status = NormalizeStatus(Str(args, "status"));
        if (string.IsNullOrWhiteSpace(taskTitle) || status is null)
            return Json(new { error = "'task' and a valid 'status' (Backlog, InProgress, Review or Done) are required." });

        // Find a task the user can access, optionally narrowed to a named project.
        var q = taskTitle.Trim().ToLower();
        var query = _context.ProjectTasks
            .Where(t => (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId))
                        && t.Title.ToLower().Contains(q));
        var projectName = Str(args, "project");
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var pn = projectName.Trim().ToLower();
            query = query.Where(t => t.Project.Name.ToLower().Contains(pn));
        }
        var task = await query
            .OrderBy(t => t.Title.Length)
            .Select(t => new { t.Id, t.Title, Project = t.Project.Name })
            .FirstOrDefaultAsync();
        if (task is null)
            return Json(new { error = $"No task you can access matches '{taskTitle}'." });

        // Goes through the task service, so move permissions (assignee-only, owner-only Done) apply.
        var result = await _tasks.ChangeStatusAsync(userId, task.Id, status);
        if (!result.Succeeded)
            return Json(new { error = result.Error });

        return Json(new { moved = true, task = task.Title, project = task.Project, status = result.Value!.Status });
    }

    private async Task<string> CreateForumTopicAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var title = Str(args, "title");
        var body = Str(args, "body");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            return Json(new { error = "Both 'title' and 'body' are required." });

        var dto = new DTOs.Forum.CreateTopicDto
        {
            Title = title.Trim(),
            Body = body.Trim(),
            Tags = StrArray(args, "tags"),
        };

        var result = await _forum.CreateTopicAsync(userId, dto);
        if (!result.Succeeded)
            return Json(new { error = result.Error });

        var topic = result.Value!;
        return Json(new { created = true, topic = new { id = topic.Id, title = topic.Title, tags = topic.Tags } });
    }

    private async Task<string> CreateProjectAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var name = Str(args, "name");
        if (string.IsNullOrWhiteSpace(name))
            return Json(new { error = "'name' is required." });

        var dto = new DTOs.Projects.SaveProjectDto
        {
            Name = name.Trim(),
            Description = Str(args, "description"),
        };

        var result = await _projects.CreateProjectAsync(userId, dto);
        if (!result.Succeeded)
            return Json(new { error = result.Error });

        var project = result.Value!;
        return Json(new { created = true, project = new { id = project.Id, name = project.Name } });
    }

    // --- helpers ---

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private static JsonElement Parse(string json)
    {
        try { return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json).RootElement.Clone(); }
        catch (JsonException) { return JsonDocument.Parse("{}").RootElement.Clone(); }
    }

    private static string? Str(JsonElement o, string prop) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static decimal? Dec(JsonElement o, string prop)
    {
        if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s)) return s;
        return null;
    }

    private static DateTime? DateOpt(JsonElement o, string prop)
    {
        var s = Str(o, prop);
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
    }

    private static string? NormalizePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority)) return null;
        return priority.Trim().ToLowerInvariant() switch
        {
            "low" => "Low",
            "high" => "High",
            "medium" => "Medium",
            _ => null,
        };
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        return status.Trim().Replace(" ", "").ToLowerInvariant() switch
        {
            "backlog" => "Backlog",
            "inprogress" or "todo" or "doing" => "InProgress",
            "review" => "Review",
            "done" or "complete" or "completed" => "Done",
            _ => null,
        };
    }

    private static List<string> StrArray(JsonElement o, string prop)
    {
        if (o.ValueKind == JsonValueKind.Object && o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array)
            return v.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        return new List<string>();
    }
}
