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
    public async Task PostMarketplaceTask_Delegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
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
}
