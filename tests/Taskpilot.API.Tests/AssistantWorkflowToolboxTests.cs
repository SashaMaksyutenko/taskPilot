using System.Text.Json;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Chat;
using Taskpilot.API.DTOs.Forum;
using Taskpilot.API.DTOs.Notes;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Taskpilot.API.Services.Assistant;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for the assistant's whole-app write tools. Business services are mocked, so no
/// real mutation happens; the tests verify the toolbox resolves names, guards missing input,
/// and delegates to the services with the right arguments.
/// </summary>
public class AssistantWorkflowToolboxTests
{
    private static AssistantWorkflowToolbox Make(
        TaskpilotDbContext ctx,
        Mock<ITaskService>? tasks = null, Mock<ITaskCommentService>? comments = null, Mock<IForumService>? forum = null,
        Mock<IChatService>? chat = null, Mock<INotificationService>? notes = null, Mock<INoteService>? note = null,
        Mock<IMarketplaceService>? market = null, Mock<IProjectService>? projects = null)
        => new(ctx,
            (tasks ?? new Mock<ITaskService>()).Object,
            (comments ?? new Mock<ITaskCommentService>()).Object,
            (forum ?? new Mock<IForumService>()).Object,
            (chat ?? new Mock<IChatService>()).Object,
            (notes ?? new Mock<INotificationService>()).Object,
            (note ?? new Mock<INoteService>()).Object,
            (market ?? new Mock<IMarketplaceService>()).Object,
            (projects ?? new Mock<IProjectService>()).Object);

    private static async Task<Guid> SeedTaskAsync(TaskpilotDbContext ctx, Guid owner, Guid projectId, string title)
    {
        var id = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = id, ProjectId = projectId, CreatorId = owner, Title = title });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task UpdateTask_PreservesUnspecifiedFields_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var project = await TestDb.AddProjectAsync(ctx, me, "Nebula");
        var taskId = await SeedTaskAsync(ctx, me, project, "Wire up auth");

        var tasks = new Mock<ITaskService>();
        tasks.Setup(t => t.GetTaskAsync(me, taskId)).ReturnsAsync(Result<TaskDto>.Ok(new TaskDto
        {
            Id = taskId, Title = "Wire up auth", Description = "old desc", Priority = "High", Tags = new() { "backend" },
        }));
        tasks.Setup(t => t.UpdateTaskAsync(me, taskId, It.IsAny<UpdateTaskDto>()))
            .ReturnsAsync(Result<TaskDto>.Ok(new TaskDto { Title = "New title", Priority = "High" }));

        var box = Make(ctx, tasks);
        var json = await box.ExecuteAsync(me, "update_task", "{\"task\":\"auth\",\"title\":\"New title\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("updated").GetBoolean());
        // Only the title changed; description and priority are carried over from the current task.
        tasks.Verify(t => t.UpdateTaskAsync(me, taskId, It.Is<UpdateTaskDto>(
            d => d.Title == "New title" && d.Description == "old desc" && d.Priority == "High")), Times.Once);
    }

    [Fact]
    public async Task UpdateTask_UnknownTask_DoesNotTouchTheService()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var tasks = new Mock<ITaskService>();

        var box = Make(ctx, tasks);
        var json = await box.ExecuteAsync(me, "update_task", "{\"task\":\"ghost\",\"title\":\"X\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
        tasks.Verify(t => t.UpdateTaskAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UpdateTaskDto>()), Times.Never);
    }

    [Fact]
    public async Task DeleteTask_ResolvesAndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var project = await TestDb.AddProjectAsync(ctx, me, "Nebula");
        var taskId = await SeedTaskAsync(ctx, me, project, "Old chore");

        var tasks = new Mock<ITaskService>();
        tasks.Setup(t => t.DeleteTaskAsync(me, taskId)).ReturnsAsync(Result.Ok());

        var box = Make(ctx, tasks);
        var json = await box.ExecuteAsync(me, "delete_task", "{\"task\":\"chore\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("deleted").GetBoolean());
        tasks.Verify(t => t.DeleteTaskAsync(me, taskId), Times.Once);
    }

    [Fact]
    public async Task AddTaskComment_ResolvesAndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var project = await TestDb.AddProjectAsync(ctx, me, "Nebula");
        var taskId = await SeedTaskAsync(ctx, me, project, "Wire up auth");

        var comments = new Mock<ITaskCommentService>();
        comments.Setup(c => c.AddAsync(me, taskId, It.IsAny<CreateCommentDto>()))
            .ReturnsAsync(Result<TaskCommentDto>.Ok(new TaskCommentDto { Id = Guid.NewGuid(), Body = "Looks good" }));

        var box = Make(ctx, comments: comments);
        var json = await box.ExecuteAsync(me, "add_task_comment", "{\"task\":\"auth\",\"body\":\"Looks good\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("commented").GetBoolean());
        comments.Verify(c => c.AddAsync(me, taskId, It.Is<CreateCommentDto>(d => d.Body == "Looks good")), Times.Once);
    }

    [Fact]
    public async Task ReplyToForumTopic_ResolvesTopic_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var topicId = Guid.NewGuid();
        ctx.ForumTopics.Add(new ForumTopic { Id = topicId, Title = "Need help with EF", Body = "…", AuthorId = me });
        await ctx.SaveChangesAsync();

        var forum = new Mock<IForumService>();
        forum.Setup(f => f.AddReplyAsync(me, It.IsAny<CreateReplyDto>()))
            .ReturnsAsync(Result<ReplyDto>.Ok(new ReplyDto { Id = Guid.NewGuid() }));

        var box = Make(ctx, forum: forum);
        var json = await box.ExecuteAsync(me, "reply_to_forum_topic", "{\"topic\":\"EF\",\"body\":\"Try AsNoTracking.\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("replied").GetBoolean());
        forum.Verify(f => f.AddReplyAsync(me, It.Is<CreateReplyDto>(
            d => d.TopicId == topicId && d.Body == "Try AsNoTracking.")), Times.Once);
    }

    [Fact]
    public async Task SendMessage_ResolvesRecipient_StartsConversationAndSends()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var convoId = Guid.NewGuid();

        var chat = new Mock<IChatService>();
        chat.Setup(c => c.StartDirectConversationAsync(me, bob))
            .ReturnsAsync(Result<ConversationDto>.Ok(new ConversationDto { Id = convoId }));
        chat.Setup(c => c.SendMessageAsync(me, It.IsAny<SendMessageDto>()))
            .ReturnsAsync(Result<MessageDto>.Ok(new MessageDto { Id = Guid.NewGuid(), Content = "Hi" }));

        var box = Make(ctx, chat: chat);
        var json = await box.ExecuteAsync(me, "send_message", "{\"recipient\":\"Bob\",\"message\":\"Hi\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("sent").GetBoolean());
        chat.Verify(c => c.StartDirectConversationAsync(me, bob), Times.Once);
        chat.Verify(c => c.SendMessageAsync(me, It.Is<SendMessageDto>(d => d.ConversationId == convoId && d.Content == "Hi")), Times.Once);
    }

    [Fact]
    public async Task MarkNotificationsRead_Delegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.MarkAllReadAsync(me)).ReturnsAsync(Result.Ok());

        var box = Make(ctx, notes: notifications);
        var json = await box.ExecuteAsync(me, "mark_notifications_read", "{}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("marked").GetBoolean());
        notifications.Verify(n => n.MarkAllReadAsync(me), Times.Once);
    }

    [Fact]
    public async Task CreateNote_Delegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var notes = new Mock<INoteService>();
        notes.Setup(n => n.CreateAsync(me, It.IsAny<SaveNoteDto>()))
            .ReturnsAsync(Result<NoteDto>.Ok(new NoteDto { Id = Guid.NewGuid(), Title = "Ideas" }));

        var box = Make(ctx, note: notes);
        var json = await box.ExecuteAsync(me, "create_note", "{\"title\":\"Ideas\",\"content\":\"Ship it\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("created").GetBoolean());
        notes.Verify(n => n.CreateAsync(me, It.Is<SaveNoteDto>(d => d.Title == "Ideas" && d.Content == "Ship it")), Times.Once);
    }

    [Fact]
    public async Task PostMarketplaceTask_AsManager_Delegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        (await ctx.Users.FindAsync(me))!.Role = Role.Manager; // posting gigs is Manager/Admin-only
        await ctx.SaveChangesAsync();
        var market = new Mock<IMarketplaceService>();
        market.Setup(m => m.CreateTaskAsync(me, It.IsAny<Taskpilot.API.DTOs.Marketplace.CreateTaskDto>()))
            .ReturnsAsync(Result<Taskpilot.API.DTOs.Marketplace.TaskDetailDto>.Ok(
                new Taskpilot.API.DTOs.Marketplace.TaskDetailDto { Id = Guid.NewGuid(), Title = "Landing page", Budget = 300m }));

        var box = Make(ctx, market: market);
        var json = await box.ExecuteAsync(me, "post_marketplace_task",
            "{\"title\":\"Landing page\",\"description\":\"A one-pager\",\"budget\":300}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("posted").GetBoolean());
        market.Verify(m => m.CreateTaskAsync(me, It.Is<Taskpilot.API.DTOs.Marketplace.CreateTaskDto>(
            d => d.Title == "Landing page" && d.Budget == 300m)), Times.Once);
    }

    [Fact]
    public async Task PostMarketplaceTask_AsDeveloper_IsBlocked()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me"); // default role is Developer
        var market = new Mock<IMarketplaceService>();

        var box = Make(ctx, market: market);
        var json = await box.ExecuteAsync(me, "post_marketplace_task",
            "{\"title\":\"Landing page\",\"description\":\"A one-pager\",\"budget\":300}");

        // A Developer cannot post gigs (Manager/Admin-only), so the service is never called.
        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
        market.Verify(m => m.CreateTaskAsync(It.IsAny<Guid>(), It.IsAny<Taskpilot.API.DTOs.Marketplace.CreateTaskDto>()), Times.Never);
    }

    [Fact]
    public async Task AddProjectMember_ResolvesProjectAndUser_Delegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var project = await TestDb.AddProjectAsync(ctx, me, "Nebula");

        var projects = new Mock<IProjectService>();
        projects.Setup(p => p.AddMemberAsync(me, project, bob, "Editor"))
            .ReturnsAsync(Result<ProjectMemberDto>.Ok(new ProjectMemberDto { UserId = bob, Name = "Bob", Role = "Editor" }));

        var box = Make(ctx, projects: projects);
        var json = await box.ExecuteAsync(me, "add_project_member", "{\"project\":\"Nebula\",\"member\":\"Bob\",\"role\":\"Editor\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("added").GetBoolean());
        projects.Verify(p => p.AddMemberAsync(me, project, bob, "Editor"), Times.Once);
    }

    [Fact]
    public async Task ArchiveProject_ResolvesAndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var project = await TestDb.AddProjectAsync(ctx, me, "Nebula");

        var projects = new Mock<IProjectService>();
        projects.Setup(p => p.SetArchivedAsync(me, project, true)).ReturnsAsync(Result.Ok());

        var box = Make(ctx, projects: projects);
        var json = await box.ExecuteAsync(me, "archive_project", "{\"project\":\"Nebula\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("archived").GetBoolean());
        projects.Verify(p => p.SetArchivedAsync(me, project, true), Times.Once);
    }

    [Fact]
    public async Task SubmitMarketplaceTask_ResolvesAssignedInProgressGig_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var poster = await TestDb.AddUserAsync(ctx, "Poster");
        var gigId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = gigId, Title = "Build a landing page", Description = "d", Budget = 500m,
            PosterId = poster, AssigneeId = me, Status = MarketplaceTaskStatus.InProgress,
        });
        await ctx.SaveChangesAsync();

        var market = new Mock<IMarketplaceService>();
        market.Setup(m => m.SubmitTaskAsync(me, gigId)).ReturnsAsync(Result.Ok());

        var box = Make(ctx, market: market);
        var json = await box.ExecuteAsync(me, "submit_marketplace_task", "{\"task\":\"landing\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("submitted").GetBoolean());
        market.Verify(m => m.SubmitTaskAsync(me, gigId), Times.Once);
    }

    [Fact]
    public async Task ApproveMarketplaceTask_ResolvesPostedGig_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var gigId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = gigId, Title = "Build a landing page", Description = "d", Budget = 500m,
            PosterId = me, Status = MarketplaceTaskStatus.InProgress,
        });
        await ctx.SaveChangesAsync();

        var market = new Mock<IMarketplaceService>();
        market.Setup(m => m.ApproveTaskAsync(me, gigId)).ReturnsAsync(Result.Ok());

        var box = Make(ctx, market: market);
        var json = await box.ExecuteAsync(me, "approve_marketplace_task", "{\"task\":\"landing\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("approved").GetBoolean());
        market.Verify(m => m.ApproveTaskAsync(me, gigId), Times.Once);
    }

    [Fact]
    public async Task DecideMarketplaceApplication_ResolvesPendingApplication_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var applicant = await TestDb.AddUserAsync(ctx, "Bob");
        var gigId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = gigId, Title = "Build a landing page", Description = "d", Budget = 500m,
            PosterId = me, Status = MarketplaceTaskStatus.Open,
        });
        var appId = Guid.NewGuid();
        ctx.TaskApplications.Add(new TaskApplication
        {
            Id = appId, TaskId = gigId, ApplicantId = applicant, CoverLetter = "hi", ProposedRate = 400m,
            Status = ApplicationStatus.Pending,
        });
        await ctx.SaveChangesAsync();

        var market = new Mock<IMarketplaceService>();
        market.Setup(m => m.DecideApplicationAsync(me, appId, true)).ReturnsAsync(Result.Ok());

        var box = Make(ctx, market: market);
        var json = await box.ExecuteAsync(me, "decide_marketplace_application",
            "{\"gig\":\"landing\",\"applicant\":\"Bob\",\"accept\":true}");

        var root = JsonDocument.Parse(json).RootElement;
        Assert.True(root.GetProperty("decided").GetBoolean());
        Assert.True(root.GetProperty("accepted").GetBoolean());
        market.Verify(m => m.DecideApplicationAsync(me, appId, true), Times.Once);
    }

    [Fact]
    public async Task ReviewMarketplaceTask_RejectsOutOfRangeStars_WithoutRating()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var gigId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = gigId, Title = "Build a landing page", Description = "d", Budget = 500m,
            PosterId = me, Status = MarketplaceTaskStatus.Completed,
        });
        await ctx.SaveChangesAsync();
        var market = new Mock<IMarketplaceService>();

        var box = Make(ctx, market: market);
        var json = await box.ExecuteAsync(me, "review_marketplace_task", "{\"task\":\"landing\",\"stars\":9}");

        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
        market.Verify(m => m.RateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ReviewMarketplaceTask_ValidStars_Delegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var gigId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = gigId, Title = "Build a landing page", Description = "d", Budget = 500m,
            PosterId = me, Status = MarketplaceTaskStatus.Completed,
        });
        await ctx.SaveChangesAsync();
        var market = new Mock<IMarketplaceService>();
        market.Setup(m => m.RateAsync(me, gigId, 5, "great")).ReturnsAsync(Result.Ok());

        var box = Make(ctx, market: market);
        var json = await box.ExecuteAsync(me, "review_marketplace_task", "{\"task\":\"landing\",\"stars\":5,\"comment\":\"great\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("reviewed").GetBoolean());
        market.Verify(m => m.RateAsync(me, gigId, 5, "great"), Times.Once);
    }

    [Fact]
    public async Task SubscribeForumTopic_WhenNotSubscribed_TogglesOn()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var topicId = Guid.NewGuid();
        ctx.ForumTopics.Add(new ForumTopic { Id = topicId, Title = "Release notes", Body = "…", AuthorId = me });
        await ctx.SaveChangesAsync();

        var forum = new Mock<IForumService>();
        forum.Setup(f => f.ToggleSubscriptionAsync(topicId, me)).ReturnsAsync(Result<bool>.Ok(true));

        var box = Make(ctx, forum: forum);
        var json = await box.ExecuteAsync(me, "subscribe_forum_topic", "{\"topic\":\"Release\"}");

        var root = JsonDocument.Parse(json).RootElement;
        Assert.True(root.GetProperty("subscribed").GetBoolean());
        Assert.True(root.GetProperty("changed").GetBoolean());
        forum.Verify(f => f.ToggleSubscriptionAsync(topicId, me), Times.Once);
    }

    [Fact]
    public async Task SubscribeForumTopic_WhenAlreadySubscribed_IsIdempotent()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var topicId = Guid.NewGuid();
        ctx.ForumTopics.Add(new ForumTopic { Id = topicId, Title = "Release notes", Body = "…", AuthorId = me });
        ctx.ForumTopicSubscriptions.Add(new ForumTopicSubscription { Id = Guid.NewGuid(), TopicId = topicId, UserId = me });
        await ctx.SaveChangesAsync();

        var forum = new Mock<IForumService>();
        var box = Make(ctx, forum: forum);
        var json = await box.ExecuteAsync(me, "subscribe_forum_topic", "{\"topic\":\"Release\",\"subscribe\":true}");

        var root = JsonDocument.Parse(json).RootElement;
        Assert.True(root.GetProperty("subscribed").GetBoolean());
        Assert.False(root.GetProperty("changed").GetBoolean());
        forum.Verify(f => f.ToggleSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task MarkForumSolution_ResolvesReplyByTopicAndSnippet_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var answerer = await TestDb.AddUserAsync(ctx, "Bob");
        var topicId = Guid.NewGuid();
        ctx.ForumTopics.Add(new ForumTopic { Id = topicId, Title = "How to seed EF", Body = "…", AuthorId = me });
        var replyId = Guid.NewGuid();
        ctx.ForumReplies.Add(new ForumReply { Id = replyId, TopicId = topicId, AuthorId = answerer, Body = "Use the InMemory provider" });
        await ctx.SaveChangesAsync();

        var forum = new Mock<IForumService>();
        forum.Setup(f => f.MarkSolutionAsync(me, replyId)).ReturnsAsync(Result.Ok());

        var box = Make(ctx, forum: forum);
        var json = await box.ExecuteAsync(me, "mark_forum_solution", "{\"topic\":\"seed EF\",\"reply\":\"InMemory\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("markedSolution").GetBoolean());
        forum.Verify(f => f.MarkSolutionAsync(me, replyId), Times.Once);
    }

    [Fact]
    public async Task ReactToForumReply_DefaultsToThumbsUp_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var topicId = Guid.NewGuid();
        ctx.ForumTopics.Add(new ForumTopic { Id = topicId, Title = "Deploy tips", Body = "…", AuthorId = me });
        var replyId = Guid.NewGuid();
        ctx.ForumReplies.Add(new ForumReply { Id = replyId, TopicId = topicId, AuthorId = me, Body = "Use blue-green deploys" });
        await ctx.SaveChangesAsync();

        var forum = new Mock<IForumService>();
        forum.Setup(f => f.ToggleReplyReactionAsync(me, replyId, "👍"))
            .ReturnsAsync(Result<List<ReactionDto>>.Ok(new List<ReactionDto>()));

        var box = Make(ctx, forum: forum);
        var json = await box.ExecuteAsync(me, "react_to_forum_reply", "{\"topic\":\"Deploy\",\"reply\":\"blue-green\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("reacted").GetBoolean());
        forum.Verify(f => f.ToggleReplyReactionAsync(me, replyId, "👍"), Times.Once);
    }

    [Fact]
    public async Task VoteForumReply_MapsDirectionToValue_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var topicId = Guid.NewGuid();
        ctx.ForumTopics.Add(new ForumTopic { Id = topicId, Title = "Deploy tips", Body = "…", AuthorId = me });
        var replyId = Guid.NewGuid();
        ctx.ForumReplies.Add(new ForumReply { Id = replyId, TopicId = topicId, AuthorId = me, Body = "Use blue-green deploys" });
        await ctx.SaveChangesAsync();

        var forum = new Mock<IForumService>();
        forum.Setup(f => f.VoteReplyAsync(me, replyId, 1)).ReturnsAsync(Result<VoteResultDto>.Ok(new VoteResultDto()));

        var box = Make(ctx, forum: forum);
        var json = await box.ExecuteAsync(me, "vote_forum_reply", "{\"topic\":\"Deploy\",\"reply\":\"blue-green\",\"direction\":\"up\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("voted").GetBoolean());
        forum.Verify(f => f.VoteReplyAsync(me, replyId, 1), Times.Once);
    }

    /// <summary>Seeds a Direct conversation between two users with one message sent by <paramref name="sender"/>.</summary>
    private static async Task<Guid> SeedDirectMessageAsync(TaskpilotDbContext ctx, Guid sender, Guid other, string body)
    {
        var convoId = Guid.NewGuid();
        ctx.Conversations.Add(new Conversation { Id = convoId, Type = ConversationType.Direct });
        ctx.ConversationParticipants.Add(new ConversationParticipant { Id = Guid.NewGuid(), ConversationId = convoId, UserId = sender });
        ctx.ConversationParticipants.Add(new ConversationParticipant { Id = Guid.NewGuid(), ConversationId = convoId, UserId = other });
        var msgId = Guid.NewGuid();
        ctx.Messages.Add(new Message { Id = msgId, ConversationId = convoId, SenderId = sender, Content = body, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        return msgId;
    }

    [Fact]
    public async Task EditLastMessage_ResolvesConversation_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var msgId = await SeedDirectMessageAsync(ctx, me, bob, "helo");

        var chat = new Mock<IChatService>();
        chat.Setup(c => c.EditMessageAsync(msgId, me, "hello"))
            .ReturnsAsync(Result<MessageDto>.Ok(new MessageDto { Id = msgId, Content = "hello" }));

        var box = Make(ctx, chat: chat);
        var json = await box.ExecuteAsync(me, "edit_last_message", "{\"recipient\":\"Bob\",\"message\":\"hello\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("edited").GetBoolean());
        chat.Verify(c => c.EditMessageAsync(msgId, me, "hello"), Times.Once);
    }

    [Fact]
    public async Task DeleteLastMessage_ResolvesConversation_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var msgId = await SeedDirectMessageAsync(ctx, me, bob, "oops");

        var chat = new Mock<IChatService>();
        chat.Setup(c => c.DeleteMessageAsync(msgId, me)).ReturnsAsync(Result.Ok());

        var box = Make(ctx, chat: chat);
        var json = await box.ExecuteAsync(me, "delete_last_message", "{\"recipient\":\"Bob\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("deleted").GetBoolean());
        chat.Verify(c => c.DeleteMessageAsync(msgId, me), Times.Once);
    }

    [Fact]
    public async Task DeleteLastMessage_NoConversation_ReturnsErrorWithoutDeleting()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        await TestDb.AddUserAsync(ctx, "Bob");
        var chat = new Mock<IChatService>();

        var box = Make(ctx, chat: chat);
        var json = await box.ExecuteAsync(me, "delete_last_message", "{\"recipient\":\"Bob\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
        chat.Verify(c => c.DeleteMessageAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }
}
