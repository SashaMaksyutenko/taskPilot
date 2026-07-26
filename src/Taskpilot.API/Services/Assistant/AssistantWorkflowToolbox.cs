using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Services;
using static Taskpilot.API.Services.Assistant.AssistantArgs;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// A second batch of write tools that let the assistant operate the rest of the app on the
/// user's behalf — editing and deleting tasks, commenting, replying on the forum, messaging
/// people, clearing notifications, taking notes, posting gigs and managing project members.
/// Like <see cref="AssistantActionsToolbox"/>, every action goes through the normal service,
/// so the same permission and validation rules the UI enforces apply here too.
/// </summary>
public class AssistantWorkflowToolbox : IAssistantToolbox
{
    private readonly TaskpilotDbContext _context;
    private readonly ITaskService _tasks;
    private readonly ITaskCommentService _comments;
    private readonly IForumService _forum;
    private readonly IChatService _chat;
    private readonly INotificationService _notifications;
    private readonly INoteService _notes;
    private readonly IMarketplaceService _marketplace;
    private readonly IProjectService _projects;

    public AssistantWorkflowToolbox(
        TaskpilotDbContext context, ITaskService tasks, ITaskCommentService comments, IForumService forum,
        IChatService chat, INotificationService notifications, INoteService notes,
        IMarketplaceService marketplace, IProjectService projects)
    {
        _context = context;
        _tasks = tasks;
        _comments = comments;
        _forum = forum;
        _chat = chat;
        _notifications = notifications;
        _notes = notes;
        _marketplace = marketplace;
        _projects = projects;
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolDefinition> Definitions { get; } = new List<ToolDefinition>
    {
        new("update_task",
            "Edits an existing task the user can access (title, description, priority, deadline or assignee). "
            + "Only the fields you pass are changed; everything else is kept. Only call this when the user asks to edit a task.",
            new
            {
                type = "object",
                properties = new
                {
                    task = new { type = "string", description = "Title (or part of it) of the task to edit." },
                    project = new { type = "string", description = "Optional project name to disambiguate." },
                    title = new { type = "string", description = "New title." },
                    description = new { type = "string", description = "New description." },
                    priority = new { type = "string", description = "New priority.", @enum = new[] { "Low", "Medium", "High" } },
                    deadline = new { type = "string", description = "New deadline as an ISO date (yyyy-MM-dd)." },
                    assignee = new { type = "string", description = "Name of a project member to (re)assign the task to." },
                },
                required = new[] { "task" },
            }),
        new("delete_task",
            "Permanently deletes one of the user's tasks. Only call this when the user clearly asks to delete a task.",
            new
            {
                type = "object",
                properties = new
                {
                    task = new { type = "string", description = "Title (or part of it) of the task to delete." },
                    project = new { type = "string", description = "Optional project name to disambiguate." },
                },
                required = new[] { "task" },
            }),
        new("add_task_comment",
            "Adds a comment to one of the user's tasks. Only call this when the user asks to comment on a task.",
            new
            {
                type = "object",
                properties = new
                {
                    task = new { type = "string", description = "Title (or part of it) of the task to comment on." },
                    project = new { type = "string", description = "Optional project name to disambiguate." },
                    body = new { type = "string", description = "The comment text." },
                },
                required = new[] { "task", "body" },
            }),
        new("reply_to_forum_topic",
            "Posts a reply to an existing forum topic. Only call this when the user asks to reply/comment on a topic.",
            new
            {
                type = "object",
                properties = new
                {
                    topic = new { type = "string", description = "Title (or part of it) of the topic to reply to." },
                    body = new { type = "string", description = "The reply text (Markdown allowed)." },
                },
                required = new[] { "topic", "body" },
            }),
        new("send_message",
            "Sends a direct chat message to another user, starting the conversation if needed. Only call this "
            + "when the user clearly asks to message someone.",
            new
            {
                type = "object",
                properties = new
                {
                    recipient = new { type = "string", description = "Name of the person to message." },
                    message = new { type = "string", description = "The message text." },
                },
                required = new[] { "recipient", "message" },
            }),
        new("mark_notifications_read",
            "Marks all of the user's notifications as read. Only call this when the user asks to clear/read their notifications.",
            new { type = "object", properties = new { }, required = Array.Empty<string>() }),
        new("create_note",
            "Creates a personal note for the user. Only call this when the user asks to jot down or save a note.",
            new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string", description = "Note title." },
                    content = new { type = "string", description = "Note body." },
                    tags = new { type = "array", items = new { type = "string" }, description = "Optional tags." },
                },
                required = new[] { "title", "content" },
            }),
        new("post_marketplace_task",
            "Posts a new gig to the marketplace on the user's behalf. Only call this when the user asks to post/create a gig.",
            new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string", description = "Gig title." },
                    description = new { type = "string", description = "What needs to be done." },
                    budget = new { type = "number", description = "Budget for the gig." },
                    skills = new { type = "string", description = "Optional required skills." },
                    deadline = new { type = "string", description = "Optional deadline as an ISO date (yyyy-MM-dd)." },
                },
                required = new[] { "title", "description", "budget" },
            }),
        new("add_project_member",
            "Adds a user to one of the projects the user owns. Only call this when the user asks to add someone to a project.",
            new
            {
                type = "object",
                properties = new
                {
                    project = new { type = "string", description = "Name of the project to add the member to." },
                    member = new { type = "string", description = "Name of the user to add." },
                    role = new { type = "string", description = "Project role.", @enum = new[] { "Viewer", "Editor" } },
                },
                required = new[] { "project", "member" },
            }),
        new("archive_project",
            "Archives (or restores) one of the projects the user owns. Only call this when the user asks to archive/restore a project.",
            new
            {
                type = "object",
                properties = new
                {
                    project = new { type = "string", description = "Name of the project to archive or restore." },
                    archived = new { type = "boolean", description = "true to archive (default), false to restore." },
                },
                required = new[] { "project" },
            }),
    };

    /// <inheritdoc />
    public Task<string> ExecuteAsync(Guid userId, string toolName, string argumentsJson) => toolName switch
    {
        "update_task" => UpdateTaskAsync(userId, argumentsJson),
        "delete_task" => DeleteTaskAsync(userId, argumentsJson),
        "add_task_comment" => AddTaskCommentAsync(userId, argumentsJson),
        "reply_to_forum_topic" => ReplyToForumTopicAsync(userId, argumentsJson),
        "send_message" => SendMessageAsync(userId, argumentsJson),
        "mark_notifications_read" => MarkNotificationsReadAsync(userId),
        "create_note" => CreateNoteAsync(userId, argumentsJson),
        "post_marketplace_task" => PostMarketplaceTaskAsync(userId, argumentsJson),
        "add_project_member" => AddProjectMemberAsync(userId, argumentsJson),
        "archive_project" => ArchiveProjectAsync(userId, argumentsJson),
        _ => Task.FromResult(Json(new { error = $"Unknown tool: {toolName}" })),
    };

    private async Task<string> UpdateTaskAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var task = await ResolveTaskAsync(userId, Str(args, "task"), Str(args, "project"));
        if (task is null) return Json(new { error = $"No task you can access matches '{Str(args, "task")}'." });

        // Load the current values so unspecified fields are preserved (UpdateTask replaces the whole task).
        var current = await _tasks.GetTaskAsync(userId, task.Id);
        if (!current.Succeeded) return Json(new { error = current.Error });
        var c = current.Value!;

        // An assignee name, if given, must be a member (or the owner) of the task's project.
        var assigneeId = c.AssigneeId;
        var assigneeName = Str(args, "assignee");
        if (!string.IsNullOrWhiteSpace(assigneeName))
        {
            var resolved = await ResolveProjectMemberAsync(task.ProjectId, assigneeName);
            if (resolved is null) return Json(new { error = $"No member named '{assigneeName}' in project '{task.Project}'." });
            assigneeId = resolved;
        }

        var dto = new DTOs.Projects.UpdateTaskDto
        {
            Title = Str(args, "title")?.Trim() is { Length: > 0 } newTitle ? newTitle : c.Title,
            Description = args.TryGetProperty("description", out _) ? Str(args, "description") : c.Description,
            Priority = NormalizePriority(Str(args, "priority")) ?? c.Priority,
            Deadline = args.TryGetProperty("deadline", out _) ? DateOpt(args, "deadline") : c.Deadline,
            AssigneeId = assigneeId,
            Tags = c.Tags,
        };

        var result = await _tasks.UpdateTaskAsync(userId, task.Id, dto);
        if (!result.Succeeded) return Json(new { error = result.Error });

        var t = result.Value!;
        return Json(new
        {
            updated = true,
            task = new { title = t.Title, project = task.Project, priority = t.Priority, deadline = t.Deadline, assignee = t.AssigneeName },
        });
    }

    private async Task<string> DeleteTaskAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var task = await ResolveTaskAsync(userId, Str(args, "task"), Str(args, "project"));
        if (task is null) return Json(new { error = $"No task you can access matches '{Str(args, "task")}'." });

        var result = await _tasks.DeleteTaskAsync(userId, task.Id);
        return result.Succeeded
            ? Json(new { deleted = true, task = task.Title, project = task.Project })
            : Json(new { error = result.Error });
    }

    private async Task<string> AddTaskCommentAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var body = Str(args, "body");
        if (string.IsNullOrWhiteSpace(body)) return Json(new { error = "'body' is required." });
        var task = await ResolveTaskAsync(userId, Str(args, "task"), Str(args, "project"));
        if (task is null) return Json(new { error = $"No task you can access matches '{Str(args, "task")}'." });

        var result = await _comments.AddAsync(userId, task.Id, new DTOs.Projects.CreateCommentDto { Body = body.Trim() });
        return result.Succeeded
            ? Json(new { commented = true, task = task.Title, project = task.Project })
            : Json(new { error = result.Error });
    }

    private async Task<string> ReplyToForumTopicAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var topicTitle = Str(args, "topic");
        var body = Str(args, "body");
        if (string.IsNullOrWhiteSpace(topicTitle) || string.IsNullOrWhiteSpace(body))
            return Json(new { error = "Both 'topic' and 'body' are required." });

        var q = topicTitle.Trim().ToLower();
        var topic = await _context.ForumTopics
            .Where(t => t.Title.ToLower().Contains(q))
            .OrderBy(t => t.Title.Length) // closest (shortest) title match
            .Select(t => new { t.Id, t.Title })
            .FirstOrDefaultAsync();
        if (topic is null) return Json(new { error = $"No forum topic matches '{topicTitle}'." });

        var result = await _forum.AddReplyAsync(userId, new DTOs.Forum.CreateReplyDto { TopicId = topic.Id, Body = body.Trim() });
        return result.Succeeded
            ? Json(new { replied = true, topic = topic.Title })
            : Json(new { error = result.Error });
    }

    private async Task<string> SendMessageAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var recipientName = Str(args, "recipient");
        var message = Str(args, "message");
        if (string.IsNullOrWhiteSpace(recipientName) || string.IsNullOrWhiteSpace(message))
            return Json(new { error = "Both 'recipient' and 'message' are required." });

        var rn = recipientName.Trim().ToLower();
        var recipient = await _context.Users
            .Where(u => u.IsActive && u.Id != userId && u.Name.ToLower().Contains(rn))
            .OrderBy(u => u.Name.Length)
            .Select(u => new { u.Id, u.Name })
            .FirstOrDefaultAsync();
        if (recipient is null) return Json(new { error = $"No user named '{recipientName}' found." });

        var convo = await _chat.StartDirectConversationAsync(userId, recipient.Id);
        if (!convo.Succeeded) return Json(new { error = convo.Error });

        var sent = await _chat.SendMessageAsync(userId, new DTOs.Chat.SendMessageDto
        {
            ConversationId = convo.Value!.Id,
            Content = message.Trim(),
        });
        return sent.Succeeded
            ? Json(new { sent = true, recipient = recipient.Name })
            : Json(new { error = sent.Error });
    }

    private async Task<string> MarkNotificationsReadAsync(Guid userId)
    {
        var result = await _notifications.MarkAllReadAsync(userId);
        return result.Succeeded ? Json(new { marked = true }) : Json(new { error = result.Error });
    }

    private async Task<string> CreateNoteAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var title = Str(args, "title");
        var content = Str(args, "content");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            return Json(new { error = "Both 'title' and 'content' are required." });

        var result = await _notes.CreateAsync(userId, new DTOs.Notes.SaveNoteDto
        {
            Title = title.Trim(),
            Content = content.Trim(),
            Tags = StrArray(args, "tags"),
        });
        return result.Succeeded
            ? Json(new { created = true, note = new { id = result.Value!.Id, title = result.Value!.Title } })
            : Json(new { error = result.Error });
    }

    private async Task<string> PostMarketplaceTaskAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var title = Str(args, "title");
        var description = Str(args, "description");
        var budget = Dec(args, "budget");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description) || budget is null)
            return Json(new { error = "'title', 'description' and a numeric 'budget' are required." });

        var result = await _marketplace.CreateTaskAsync(userId, new DTOs.Marketplace.CreateTaskDto
        {
            Title = title.Trim(),
            Description = description.Trim(),
            Budget = budget.Value,
            RequiredSkills = Str(args, "skills"),
            Deadline = DateOpt(args, "deadline"),
        });
        return result.Succeeded
            ? Json(new { posted = true, gig = new { id = result.Value!.Id, title = result.Value!.Title, budget = result.Value!.Budget } })
            : Json(new { error = result.Error });
    }

    private async Task<string> AddProjectMemberAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var project = await ResolveProjectAsync(userId, Str(args, "project"));
        if (project is null) return Json(new { error = $"No project you can access matches '{Str(args, "project")}'." });

        var memberName = Str(args, "member");
        if (string.IsNullOrWhiteSpace(memberName)) return Json(new { error = "'member' is required." });
        var mn = memberName.Trim().ToLower();
        var target = await _context.Users
            .Where(u => u.IsActive && u.Name.ToLower().Contains(mn))
            .OrderBy(u => u.Name.Length)
            .Select(u => new { u.Id, u.Name })
            .FirstOrDefaultAsync();
        if (target is null) return Json(new { error = $"No user named '{memberName}' found." });

        var role = Str(args, "role") is { Length: > 0 } r ? r : "Viewer";
        var result = await _projects.AddMemberAsync(userId, project.Id, target.Id, role);
        return result.Succeeded
            ? Json(new { added = true, project = project.Name, member = target.Name, role })
            : Json(new { error = result.Error });
    }

    private async Task<string> ArchiveProjectAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var project = await ResolveProjectAsync(userId, Str(args, "project"));
        if (project is null) return Json(new { error = $"No project you can access matches '{Str(args, "project")}'." });

        var archived = Bool(args, "archived") ?? true;
        var result = await _projects.SetArchivedAsync(userId, project.Id, archived);
        return result.Succeeded
            ? Json(new { archived, project = project.Name })
            : Json(new { error = result.Error });
    }

    // --- resolution helpers ---

    private record ResolvedTask(Guid Id, string Title, Guid ProjectId, string Project);

    private record ResolvedProject(Guid Id, string Name);

    /// <summary>Finds a task the user can access by (partial) title, optionally within a named project.</summary>
    private async Task<ResolvedTask?> ResolveTaskAsync(Guid userId, string? title, string? projectName)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var q = title.Trim().ToLower();
        var query = _context.ProjectTasks
            .Where(t => (t.Project.OwnerId == userId || t.Project.Members.Any(m => m.UserId == userId))
                        && t.Title.ToLower().Contains(q));
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var pn = projectName.Trim().ToLower();
            query = query.Where(t => t.Project.Name.ToLower().Contains(pn));
        }
        return await query
            .OrderBy(t => t.Title.Length)
            .Select(t => new ResolvedTask(t.Id, t.Title, t.ProjectId, t.Project.Name))
            .FirstOrDefaultAsync();
    }

    private async Task<ResolvedProject?> ResolveProjectAsync(Guid userId, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var q = name.Trim().ToLower();
        return await _context.Projects
            .Where(p => (p.OwnerId == userId || p.Members.Any(m => m.UserId == userId)) && p.Name.ToLower().Contains(q))
            .OrderBy(p => p.Name.Length)
            .Select(p => new ResolvedProject(p.Id, p.Name))
            .FirstOrDefaultAsync();
    }

    private async Task<Guid?> ResolveProjectMemberAsync(Guid projectId, string name)
    {
        var n = name.Trim().ToLower();
        var member = await _context.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.User.Name.ToLower().Contains(n))
            .Select(m => (Guid?)m.UserId)
            .FirstOrDefaultAsync();
        return member ?? await _context.Projects
            .Where(p => p.Id == projectId && p.Owner.Name.ToLower().Contains(n))
            .Select(p => (Guid?)p.OwnerId)
            .FirstOrDefaultAsync();
    }
}
