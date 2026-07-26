using System.Text.Json;
using Moq;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Auth;
using Taskpilot.API.DTOs.Bookmarks;
using Taskpilot.API.DTOs.Users;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Taskpilot.API.Services.Assistant;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for the assistant's profile/bookmark tools. The services are mocked; the tests
/// verify partial-update semantics (unspecified fields kept, skills appended) and that
/// bookmarking resolves the named task/topic into the right toggle payload.
/// </summary>
public class AssistantProfileToolboxTests
{
    private static AssistantProfileToolbox Make(
        TaskpilotDbContext ctx, Mock<IUserService>? users = null, Mock<IBookmarkService>? bookmarks = null,
        Mock<ISavedSearchService>? savedSearches = null, Mock<INotificationService>? notifications = null)
        => new(ctx, (users ?? new Mock<IUserService>()).Object, (bookmarks ?? new Mock<IBookmarkService>()).Object,
            (savedSearches ?? new Mock<ISavedSearchService>()).Object, (notifications ?? new Mock<INotificationService>()).Object);

    [Fact]
    public async Task UpdateProfile_AddSkill_AppendsAndPreservesOtherFields()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var user = (await ctx.Users.FindAsync(me))!;
        user.Title = "Developer";
        user.Skills = new List<string> { "C#" };
        await ctx.SaveChangesAsync();

        var users = new Mock<IUserService>();
        users.Setup(u => u.UpdateProfileAsync(me, It.IsAny<UpdateProfileDto>()))
            .ReturnsAsync(Result<UserDto>.Ok(new UserDto { Id = me, Name = "Me" }));

        var box = Make(ctx, users);
        var json = await box.ExecuteAsync(me, "update_profile", "{\"add_skills\":[\"Python\"]}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("updated").GetBoolean());
        // The existing skill and title are preserved; the new skill is appended.
        users.Verify(u => u.UpdateProfileAsync(me, It.Is<UpdateProfileDto>(
            d => d.Name == "Me" && d.Title == "Developer"
                 && d.Skills.Contains("C#") && d.Skills.Contains("Python"))), Times.Once);
    }

    [Fact]
    public async Task UpdateProfile_ReplacesSkillsList_WhenSkillsProvided()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var user = (await ctx.Users.FindAsync(me))!;
        user.Skills = new List<string> { "C#", "SQL" };
        await ctx.SaveChangesAsync();

        var users = new Mock<IUserService>();
        users.Setup(u => u.UpdateProfileAsync(me, It.IsAny<UpdateProfileDto>()))
            .ReturnsAsync(Result<UserDto>.Ok(new UserDto { Id = me, Name = "Me" }));

        var box = Make(ctx, users);
        var json = await box.ExecuteAsync(me, "update_profile", "{\"skills\":[\"Go\"]}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("updated").GetBoolean());
        users.Verify(u => u.UpdateProfileAsync(me, It.Is<UpdateProfileDto>(
            d => d.Skills.Count == 1 && d.Skills.Contains("Go"))), Times.Once);
    }

    [Fact]
    public async Task BookmarkItem_ResolvesTask_AndTogglesWithTaskPayload()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var project = await TestDb.AddProjectAsync(ctx, me, "Nebula");
        var taskId = Guid.NewGuid();
        ctx.ProjectTasks.Add(new ProjectTask { Id = taskId, ProjectId = project, CreatorId = me, Title = "Wire up auth" });
        await ctx.SaveChangesAsync();

        var bookmarks = new Mock<IBookmarkService>();
        bookmarks.Setup(b => b.ToggleAsync(me, It.IsAny<ToggleBookmarkDto>())).ReturnsAsync(Result<bool>.Ok(true));

        var box = Make(ctx, bookmarks: bookmarks);
        var json = await box.ExecuteAsync(me, "bookmark_item", "{\"task\":\"auth\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("bookmarked").GetBoolean());
        bookmarks.Verify(b => b.ToggleAsync(me, It.Is<ToggleBookmarkDto>(
            d => d.Type == "Task" && d.EntityId == taskId && d.Link.Contains(taskId.ToString()))), Times.Once);
    }

    [Fact]
    public async Task BookmarkItem_ResolvesTopic_AndTogglesWithTopicPayload()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var topicId = Guid.NewGuid();
        ctx.ForumTopics.Add(new ForumTopic { Id = topicId, Title = "Release notes", Body = "…", AuthorId = me });
        await ctx.SaveChangesAsync();

        var bookmarks = new Mock<IBookmarkService>();
        bookmarks.Setup(b => b.ToggleAsync(me, It.IsAny<ToggleBookmarkDto>())).ReturnsAsync(Result<bool>.Ok(true));

        var box = Make(ctx, bookmarks: bookmarks);
        var json = await box.ExecuteAsync(me, "bookmark_item", "{\"topic\":\"Release\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("bookmarked").GetBoolean());
        bookmarks.Verify(b => b.ToggleAsync(me, It.Is<ToggleBookmarkDto>(
            d => d.Type == "Topic" && d.EntityId == topicId && d.Link == $"/forum/{topicId}")), Times.Once);
    }

    [Fact]
    public async Task BookmarkItem_NoTaskOrTopic_ReturnsError()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var bookmarks = new Mock<IBookmarkService>();

        var box = Make(ctx, bookmarks: bookmarks);
        var json = await box.ExecuteAsync(me, "bookmark_item", "{}");

        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
        bookmarks.Verify(b => b.ToggleAsync(It.IsAny<Guid>(), It.IsAny<ToggleBookmarkDto>()), Times.Never);
    }

    [Fact]
    public async Task SaveSearch_Delegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var searches = new Mock<ISavedSearchService>();
        searches.Setup(s => s.CreateAsync(me, It.IsAny<Taskpilot.API.DTOs.Search.CreateSavedSearchDto>()))
            .ReturnsAsync(Result<Taskpilot.API.DTOs.Search.SavedSearchDto>.Ok(
                new Taskpilot.API.DTOs.Search.SavedSearchDto { Id = Guid.NewGuid(), Name = "Overdue" }));

        var box = Make(ctx, savedSearches: searches);
        var json = await box.ExecuteAsync(me, "save_search", "{\"name\":\"Overdue\",\"query\":\"status:overdue\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("saved").GetBoolean());
        searches.Verify(s => s.CreateAsync(me, It.Is<Taskpilot.API.DTOs.Search.CreateSavedSearchDto>(
            d => d.Name == "Overdue" && d.Query == "status:overdue")), Times.Once);
    }

    [Fact]
    public async Task SetNotificationDigest_Delegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.SetDigestFrequencyAsync(me, "Daily")).ReturnsAsync(Result<string>.Ok("Daily"));

        var box = Make(ctx, notifications: notifications);
        var json = await box.ExecuteAsync(me, "set_notification_digest", "{\"frequency\":\"Daily\"}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("updated").GetBoolean());
        notifications.Verify(n => n.SetDigestFrequencyAsync(me, "Daily"), Times.Once);
    }

    [Fact]
    public async Task SetQuietHours_ParsesHours_AndDelegates()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.SetQuietHoursAsync(me, It.IsAny<Taskpilot.API.DTOs.Notifications.QuietHoursDto>()))
            .ReturnsAsync((Guid _, Taskpilot.API.DTOs.Notifications.QuietHoursDto d) =>
                Result<Taskpilot.API.DTOs.Notifications.QuietHoursDto>.Ok(d));

        var box = Make(ctx, notifications: notifications);
        var json = await box.ExecuteAsync(me, "set_quiet_hours", "{\"enabled\":true,\"start\":23,\"end\":7}");

        Assert.True(JsonDocument.Parse(json).RootElement.GetProperty("updated").GetBoolean());
        notifications.Verify(n => n.SetQuietHoursAsync(me, It.Is<Taskpilot.API.DTOs.Notifications.QuietHoursDto>(
            d => d.Enabled && d.Start == 23 && d.End == 7)), Times.Once);
    }

    [Fact]
    public async Task SetQuietHours_RejectsOutOfRangeHour()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var notifications = new Mock<INotificationService>();

        var box = Make(ctx, notifications: notifications);
        var json = await box.ExecuteAsync(me, "set_quiet_hours", "{\"enabled\":true,\"start\":30,\"end\":7}");

        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
        notifications.Verify(n => n.SetQuietHoursAsync(It.IsAny<Guid>(), It.IsAny<Taskpilot.API.DTOs.Notifications.QuietHoursDto>()), Times.Never);
    }
}
