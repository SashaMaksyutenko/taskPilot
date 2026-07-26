using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Services;
using static Taskpilot.API.Services.Assistant.AssistantArgs;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// Write tools for the user's own account and personal collections — editing their profile
/// (bio, title, skills, links) and bookmarking tasks or forum topics. Like the other write
/// toolboxes, everything goes through the normal services, so their validation applies and the
/// tools only ever touch the calling user's own data.
/// </summary>
public class AssistantProfileToolbox : IAssistantToolbox
{
    private readonly TaskpilotDbContext _context;
    private readonly IUserService _users;
    private readonly IBookmarkService _bookmarks;

    public AssistantProfileToolbox(TaskpilotDbContext context, IUserService users, IBookmarkService bookmarks)
    {
        _context = context;
        _users = users;
        _bookmarks = bookmarks;
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolDefinition> Definitions { get; } = new List<ToolDefinition>
    {
        new("update_profile",
            "Updates the user's own profile. Only the fields you pass are changed; everything else is kept. "
            + "Use 'add_skills' to append skills, or 'skills' to replace the whole list. Only call this when the "
            + "user asks to change their profile.",
            new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "Display name." },
                    title = new { type = "string", description = "Headline/role, e.g. \"Senior Developer\"." },
                    bio = new { type = "string", description = "Short biography." },
                    location = new { type = "string", description = "Location." },
                    add_skills = new { type = "array", items = new { type = "string" }, description = "Skills to add to the existing list." },
                    skills = new { type = "array", items = new { type = "string" }, description = "Replaces the whole skills list." },
                    website = new { type = "string", description = "Website URL." },
                    linkedIn = new { type = "string", description = "LinkedIn URL." },
                    gitHub = new { type = "string", description = "GitHub URL." },
                    phone = new { type = "string", description = "Phone number." },
                    showEmail = new { type = "boolean", description = "Whether to show the email on the public profile." },
                },
                required = Array.Empty<string>(),
            }),
        new("bookmark_item",
            "Bookmarks (or removes the bookmark from) one of the user's tasks or a forum topic. Only call this when "
            + "the user asks to bookmark/save or un-bookmark something.",
            new
            {
                type = "object",
                properties = new
                {
                    task = new { type = "string", description = "Title (or part of it) of a task to bookmark." },
                    topic = new { type = "string", description = "Title (or part of it) of a forum topic to bookmark." },
                },
                required = Array.Empty<string>(),
            }),
    };

    /// <inheritdoc />
    public Task<string> ExecuteAsync(Guid userId, string toolName, string argumentsJson) => toolName switch
    {
        "update_profile" => UpdateProfileAsync(userId, argumentsJson),
        "bookmark_item" => BookmarkItemAsync(userId, argumentsJson),
        _ => Task.FromResult(Json(new { error = $"Unknown tool: {toolName}" })),
    };

    private async Task<string> UpdateProfileAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);

        // Load the current profile so unspecified fields are preserved (UpdateProfile replaces the whole profile).
        var current = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Name, u.Title, u.Bio, u.Location, u.Skills, u.Website, u.LinkedIn, u.GitHub, u.Phone, u.ShowEmail,
            })
            .FirstOrDefaultAsync();
        if (current is null) return Json(new { error = "Profile not found." });

        // Skills: 'skills' replaces the list, 'add_skills' appends (case-insensitively deduped).
        var skills = new List<string>(current.Skills);
        var replacement = StrArray(args, "skills");
        if (args.TryGetProperty("skills", out _)) skills = replacement;
        foreach (var s in StrArray(args, "add_skills"))
            if (!skills.Any(x => string.Equals(x, s, StringComparison.OrdinalIgnoreCase)))
                skills.Add(s);

        var dto = new DTOs.Users.UpdateProfileDto
        {
            Name = Str(args, "name")?.Trim() is { Length: > 0 } n ? n : current.Name,
            Title = args.TryGetProperty("title", out _) ? Str(args, "title") : current.Title,
            Bio = args.TryGetProperty("bio", out _) ? Str(args, "bio") : current.Bio,
            Location = args.TryGetProperty("location", out _) ? Str(args, "location") : current.Location,
            Skills = skills,
            Website = args.TryGetProperty("website", out _) ? Str(args, "website") : current.Website,
            LinkedIn = args.TryGetProperty("linkedIn", out _) ? Str(args, "linkedIn") : current.LinkedIn,
            GitHub = args.TryGetProperty("gitHub", out _) ? Str(args, "gitHub") : current.GitHub,
            Phone = args.TryGetProperty("phone", out _) ? Str(args, "phone") : current.Phone,
            ShowEmail = Bool(args, "showEmail") ?? current.ShowEmail,
        };

        var result = await _users.UpdateProfileAsync(userId, dto);
        if (!result.Succeeded) return Json(new { error = result.Error });

        return Json(new { updated = true, profile = new { name = dto.Name, title = dto.Title, skills = dto.Skills } });
    }

    private async Task<string> BookmarkItemAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var taskName = Str(args, "task");
        var topicName = Str(args, "topic");

        DTOs.Bookmarks.ToggleBookmarkDto dto;
        string label;
        if (!string.IsNullOrWhiteSpace(taskName))
        {
            var q = taskName.Trim().ToLower();
            var task = await _context.ProjectTasks
                .Where(t => (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId))
                            && t.Title.ToLower().Contains(q))
                .OrderBy(t => t.Title.Length)
                .Select(t => new { t.Id, t.Title, t.ProjectId })
                .FirstOrDefaultAsync();
            if (task is null) return Json(new { error = $"No task you can access matches '{taskName}'." });
            dto = new DTOs.Bookmarks.ToggleBookmarkDto
            {
                Type = "Task", EntityId = task.Id, Title = task.Title, Link = $"/projects/{task.ProjectId}?task={task.Id}",
            };
            label = task.Title;
        }
        else if (!string.IsNullOrWhiteSpace(topicName))
        {
            var q = topicName.Trim().ToLower();
            var topic = await _context.ForumTopics
                .Where(t => t.Title.ToLower().Contains(q))
                .OrderBy(t => t.Title.Length)
                .Select(t => new { t.Id, t.Title })
                .FirstOrDefaultAsync();
            if (topic is null) return Json(new { error = $"No forum topic matches '{topicName}'." });
            dto = new DTOs.Bookmarks.ToggleBookmarkDto
            {
                Type = "Topic", EntityId = topic.Id, Title = topic.Title, Link = $"/forum/{topic.Id}",
            };
            label = topic.Title;
        }
        else
        {
            return Json(new { error = "Provide a 'task' or a 'topic' to bookmark." });
        }

        var result = await _bookmarks.ToggleAsync(userId, dto);
        return result.Succeeded
            ? Json(new { bookmarked = result.Value, item = label })
            : Json(new { error = result.Error });
    }
}
