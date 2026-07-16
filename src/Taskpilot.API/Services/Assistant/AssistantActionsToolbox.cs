using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Marketplace;
using Taskpilot.API.Services;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// Write tools the assistant can run on the user's behalf when explicitly asked
/// (create a task, apply to a marketplace gig). Every action goes through the normal
/// services, so their permission and validation rules apply — the assistant cannot do
/// anything the user could not do themselves through the UI.
/// </summary>
public class AssistantActionsToolbox : IAssistantToolbox
{
    private readonly TaskpilotDbContext _context;
    private readonly ITaskService _tasks;
    private readonly IMarketplaceService _marketplace;

    public AssistantActionsToolbox(TaskpilotDbContext context, ITaskService tasks, IMarketplaceService marketplace)
    {
        _context = context;
        _tasks = tasks;
        _marketplace = marketplace;
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
    };

    /// <inheritdoc />
    public Task<string> ExecuteAsync(Guid userId, string toolName, string argumentsJson) => toolName switch
    {
        "create_task" => CreateTaskAsync(userId, argumentsJson),
        "apply_to_marketplace_task" => ApplyToMarketplaceTaskAsync(userId, argumentsJson),
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
}
