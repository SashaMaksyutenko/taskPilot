using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Data;
using Taskpilot.API.Services;
using static Taskpilot.API.Services.Assistant.AssistantArgs;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// The broad set of write tools that let the assistant operate the rest of the app on the
/// user's behalf — editing and deleting tasks, commenting, replying on and following forum
/// topics, marking solutions, reacting to and voting on replies, messaging people, clearing
/// notifications, taking notes, managing project members, and running the marketplace lifecycle
/// (posting, submitting, approving, deciding applications and reviewing). Like <see cref="AssistantActionsToolbox"/>, every
/// action goes through the normal service, so the same permission and validation rules the UI
/// enforces apply here too.
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
        new("submit_marketplace_task",
            "Submits the user's finished work on a marketplace gig they are assigned to, sending it to the poster "
            + "for approval. Only call this when the user says their work is done/ready.",
            new
            {
                type = "object",
                properties = new
                {
                    task = new { type = "string", description = "Title (or part of it) of the gig the user is working on." },
                },
                required = new[] { "task" },
            }),
        new("approve_marketplace_task",
            "Approves the submitted work on a gig the user posted, marking it complete. Only call this when the user "
            + "clearly asks to approve/accept the delivered work.",
            new
            {
                type = "object",
                properties = new
                {
                    task = new { type = "string", description = "Title (or part of it) of the gig the user posted." },
                },
                required = new[] { "task" },
            }),
        new("decide_marketplace_application",
            "Accepts or rejects a pending application to a gig the user posted. Only call this when the user asks to "
            + "accept/reject a specific applicant.",
            new
            {
                type = "object",
                properties = new
                {
                    gig = new { type = "string", description = "Title (or part of it) of the gig the user posted." },
                    applicant = new { type = "string", description = "Name of the applicant to decide on." },
                    accept = new { type = "boolean", description = "true to accept, false to reject." },
                },
                required = new[] { "gig", "applicant", "accept" },
            }),
        new("review_marketplace_task",
            "Leaves a star rating (1-5) and optional comment for a completed gig the user took part in. Only call this "
            + "when the user asks to review/rate a gig.",
            new
            {
                type = "object",
                properties = new
                {
                    task = new { type = "string", description = "Title (or part of it) of the completed gig." },
                    stars = new { type = "integer", description = "Rating from 1 to 5." },
                    comment = new { type = "string", description = "Optional written review." },
                },
                required = new[] { "task", "stars" },
            }),
        new("subscribe_forum_topic",
            "Subscribes the user to (or unsubscribes them from) a forum topic so they get notified of new replies. "
            + "Only call this when the user asks to follow/unfollow a topic.",
            new
            {
                type = "object",
                properties = new
                {
                    topic = new { type = "string", description = "Title (or part of it) of the topic." },
                    subscribe = new { type = "boolean", description = "true to subscribe (default), false to unsubscribe." },
                },
                required = new[] { "topic" },
            }),
        new("mark_forum_solution",
            "Marks a reply as the accepted solution on one of the user's own forum topics. Only call this when the "
            + "user asks to mark/accept an answer as the solution.",
            new
            {
                type = "object",
                properties = new
                {
                    topic = new { type = "string", description = "Title (or part of it) of the user's topic." },
                    reply = new { type = "string", description = "A distinctive snippet of the reply to mark as the solution." },
                },
                required = new[] { "topic", "reply" },
            }),
        new("react_to_forum_reply",
            "Toggles an emoji reaction on a forum reply. Only call this when the user asks to react to a reply.",
            new
            {
                type = "object",
                properties = new
                {
                    topic = new { type = "string", description = "Title (or part of it) of the topic the reply is on." },
                    reply = new { type = "string", description = "A distinctive snippet of the reply to react to." },
                    emoji = new { type = "string", description = "The emoji to toggle (defaults to 👍)." },
                },
                required = new[] { "topic", "reply" },
            }),
        new("vote_forum_reply",
            "Upvotes or downvotes a forum reply. Only call this when the user clearly asks to up/down-vote a reply.",
            new
            {
                type = "object",
                properties = new
                {
                    topic = new { type = "string", description = "Title (or part of it) of the topic the reply is on." },
                    reply = new { type = "string", description = "A distinctive snippet of the reply to vote on." },
                    direction = new { type = "string", description = "Vote direction.", @enum = new[] { "up", "down" } },
                },
                required = new[] { "topic", "reply", "direction" },
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
        "submit_marketplace_task" => SubmitMarketplaceTaskAsync(userId, argumentsJson),
        "approve_marketplace_task" => ApproveMarketplaceTaskAsync(userId, argumentsJson),
        "decide_marketplace_application" => DecideMarketplaceApplicationAsync(userId, argumentsJson),
        "review_marketplace_task" => ReviewMarketplaceTaskAsync(userId, argumentsJson),
        "subscribe_forum_topic" => SubscribeForumTopicAsync(userId, argumentsJson),
        "mark_forum_solution" => MarkForumSolutionAsync(userId, argumentsJson),
        "react_to_forum_reply" => ReactToForumReplyAsync(userId, argumentsJson),
        "vote_forum_reply" => VoteForumReplyAsync(userId, argumentsJson),
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

        // Posting a gig is Manager/Admin-only (enforced on the controller via RBAC); the service itself
        // does not re-check, so the assistant must apply the same gate rather than let it be bypassed.
        var role = await _context.Users.Where(u => u.Id == userId).Select(u => u.Role).FirstOrDefaultAsync();
        if (role is not (Models.Role.Manager or Models.Role.Admin))
            return Json(new { error = "Only managers and admins can post marketplace gigs." });

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

    private async Task<string> SubmitMarketplaceTaskAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var title = Str(args, "task");
        if (string.IsNullOrWhiteSpace(title)) return Json(new { error = "'task' is required." });

        var q = title.Trim().ToLower();
        var gig = await _context.MarketplaceTasks
            .Where(m => m.AssigneeId == userId && m.Status == Models.MarketplaceTaskStatus.InProgress && m.Title.ToLower().Contains(q))
            .OrderBy(m => m.Title.Length)
            .Select(m => new { m.Id, m.Title })
            .FirstOrDefaultAsync();
        if (gig is null) return Json(new { error = $"No in-progress gig assigned to you matches '{title}'." });

        var result = await _marketplace.SubmitTaskAsync(userId, gig.Id);
        return result.Succeeded
            ? Json(new { submitted = true, gig = gig.Title })
            : Json(new { error = result.Error });
    }

    private async Task<string> ApproveMarketplaceTaskAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var title = Str(args, "task");
        if (string.IsNullOrWhiteSpace(title)) return Json(new { error = "'task' is required." });

        var q = title.Trim().ToLower();
        var gig = await _context.MarketplaceTasks
            .Where(m => m.PosterId == userId && m.Title.ToLower().Contains(q))
            .OrderBy(m => m.Title.Length)
            .Select(m => new { m.Id, m.Title })
            .FirstOrDefaultAsync();
        if (gig is null) return Json(new { error = $"No gig you posted matches '{title}'." });

        var result = await _marketplace.ApproveTaskAsync(userId, gig.Id);
        return result.Succeeded
            ? Json(new { approved = true, gig = gig.Title })
            : Json(new { error = result.Error });
    }

    private async Task<string> DecideMarketplaceApplicationAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var gigTitle = Str(args, "gig");
        var applicantName = Str(args, "applicant");
        var accept = Bool(args, "accept");
        if (string.IsNullOrWhiteSpace(gigTitle) || string.IsNullOrWhiteSpace(applicantName) || accept is null)
            return Json(new { error = "'gig', 'applicant' and 'accept' (true/false) are required." });

        var gq = gigTitle.Trim().ToLower();
        var an = applicantName.Trim().ToLower();
        // Only pending applications to gigs THIS user posted are decidable.
        var application = await _context.TaskApplications
            .Where(a => a.Status == Models.ApplicationStatus.Pending
                        && a.Task.PosterId == userId
                        && a.Task.Title.ToLower().Contains(gq)
                        && a.Applicant.Name.ToLower().Contains(an))
            .Select(a => new { a.Id, Applicant = a.Applicant.Name, Gig = a.Task.Title })
            .FirstOrDefaultAsync();
        if (application is null)
            return Json(new { error = $"No pending application from '{applicantName}' on a gig matching '{gigTitle}'." });

        var result = await _marketplace.DecideApplicationAsync(userId, application.Id, accept.Value);
        return result.Succeeded
            ? Json(new { decided = true, gig = application.Gig, applicant = application.Applicant, accepted = accept.Value })
            : Json(new { error = result.Error });
    }

    private async Task<string> ReviewMarketplaceTaskAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var title = Str(args, "task");
        var stars = Int(args, "stars");
        if (string.IsNullOrWhiteSpace(title)) return Json(new { error = "'task' is required." });
        if (stars is null or < 1 or > 5) return Json(new { error = "'stars' must be an integer from 1 to 5." });

        var q = title.Trim().ToLower();
        var gig = await _context.MarketplaceTasks
            .Where(m => (m.PosterId == userId || m.AssigneeId == userId) && m.Title.ToLower().Contains(q))
            .OrderBy(m => m.Title.Length)
            .Select(m => new { m.Id, m.Title })
            .FirstOrDefaultAsync();
        if (gig is null) return Json(new { error = $"No gig you took part in matches '{title}'." });

        var result = await _marketplace.RateAsync(userId, gig.Id, stars.Value, Str(args, "comment"));
        return result.Succeeded
            ? Json(new { reviewed = true, gig = gig.Title, stars = stars.Value })
            : Json(new { error = result.Error });
    }

    private async Task<string> SubscribeForumTopicAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var title = Str(args, "topic");
        if (string.IsNullOrWhiteSpace(title)) return Json(new { error = "'topic' is required." });

        var q = title.Trim().ToLower();
        var topic = await _context.ForumTopics
            .Where(t => t.Title.ToLower().Contains(q))
            .OrderBy(t => t.Title.Length)
            .Select(t => new { t.Id, t.Title })
            .FirstOrDefaultAsync();
        if (topic is null) return Json(new { error = $"No forum topic matches '{title}'." });

        // ToggleSubscription flips the state; only toggle when it differs from the requested one so the tool is idempotent.
        var desired = Bool(args, "subscribe") ?? true;
        var currentlySubscribed = await _context.ForumTopicSubscriptions
            .AnyAsync(s => s.TopicId == topic.Id && s.UserId == userId);
        if (currentlySubscribed == desired)
            return Json(new { subscribed = desired, topic = topic.Title, changed = false });

        var result = await _forum.ToggleSubscriptionAsync(topic.Id, userId);
        return result.Succeeded
            ? Json(new { subscribed = result.Value, topic = topic.Title, changed = true })
            : Json(new { error = result.Error });
    }

    private async Task<string> MarkForumSolutionAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var reply = await ResolveReplyAsync(Str(args, "topic"), Str(args, "reply"));
        if (reply is null) return Json(new { error = $"No reply matching '{Str(args, "reply")}' found on a topic matching '{Str(args, "topic")}'." });

        // The service enforces that only the topic author (or an admin) can mark a solution.
        var result = await _forum.MarkSolutionAsync(userId, reply.Id);
        return result.Succeeded
            ? Json(new { markedSolution = true, topic = reply.Topic })
            : Json(new { error = result.Error });
    }

    private async Task<string> ReactToForumReplyAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var reply = await ResolveReplyAsync(Str(args, "topic"), Str(args, "reply"));
        if (reply is null) return Json(new { error = $"No reply matching '{Str(args, "reply")}' found on a topic matching '{Str(args, "topic")}'." });

        var emoji = Str(args, "emoji") is { Length: > 0 } e ? e.Trim() : "👍";
        var result = await _forum.ToggleReplyReactionAsync(userId, reply.Id, emoji);
        return result.Succeeded
            ? Json(new { reacted = true, topic = reply.Topic, emoji })
            : Json(new { error = result.Error });
    }

    private async Task<string> VoteForumReplyAsync(Guid userId, string argsJson)
    {
        var args = Parse(argsJson);
        var direction = Str(args, "direction")?.Trim().ToLowerInvariant();
        if (direction is not ("up" or "down"))
            return Json(new { error = "'direction' must be 'up' or 'down'." });
        var reply = await ResolveReplyAsync(Str(args, "topic"), Str(args, "reply"));
        if (reply is null) return Json(new { error = $"No reply matching '{Str(args, "reply")}' found on a topic matching '{Str(args, "topic")}'." });

        var result = await _forum.VoteReplyAsync(userId, reply.Id, direction == "up" ? 1 : -1);
        return result.Succeeded
            ? Json(new { voted = true, topic = reply.Topic, direction })
            : Json(new { error = result.Error });
    }

    // --- resolution helpers ---

    private record ResolvedReply(Guid Id, string Topic);

    /// <summary>Finds a non-deleted reply by a snippet of its body within a topic matched by title.</summary>
    private async Task<ResolvedReply?> ResolveReplyAsync(string? topicTitle, string? snippet)
    {
        if (string.IsNullOrWhiteSpace(topicTitle) || string.IsNullOrWhiteSpace(snippet)) return null;
        var tq = topicTitle.Trim().ToLower();
        var rq = snippet.Trim().ToLower();
        return await _context.ForumReplies
            .Where(r => !r.IsDeleted && r.Topic.Title.ToLower().Contains(tq) && r.Body.ToLower().Contains(rq))
            .OrderBy(r => r.Body.Length)
            .Select(r => new ResolvedReply(r.Id, r.Topic.Title))
            .FirstOrDefaultAsync();
    }


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
