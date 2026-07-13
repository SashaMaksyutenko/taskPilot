using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="ForumService"/> reply editing over the in-memory provider.</summary>
public class ForumServiceTests
{
    private static ForumService Create(TaskpilotDbContext ctx) => CreateWithMock(ctx).svc;

    private static (ForumService svc, Mock<INotificationService> notifications) CreateWithMock(TaskpilotDbContext ctx)
    {
        var notifications = new Mock<INotificationService>();
        return (new ForumService(ctx, notifications.Object, new Mock<IReputationService>().Object, NullLogger<ForumService>.Instance), notifications);
    }

    private static async Task<(Guid topicId, Guid replyId)> SeedTopicWithReplyAsync(
        TaskpilotDbContext ctx, Guid authorId, Guid replyAuthorId)
    {
        var topicId = Guid.NewGuid();
        ctx.ForumTopics.Add(new ForumTopic
        {
            Id = topicId, Title = "T", Body = "B", AuthorId = authorId, CreatedAt = DateTime.UtcNow,
        });
        var replyId = Guid.NewGuid();
        ctx.ForumReplies.Add(new ForumReply
        {
            Id = replyId, TopicId = topicId, AuthorId = replyAuthorId, Body = "original", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return (topicId, replyId);
    }

    [Fact]
    public async Task EditReply_ByAuthor_UpdatesBodyAndStampsUpdatedAt()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);

        var result = await Create(ctx).EditReplyAsync(author, replyId, "  edited body  ", isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal("edited body", result.Value!.Body);
        var stored = await ctx.ForumReplies.FirstAsync(r => r.Id == replyId);
        Assert.Equal("edited body", stored.Body);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task EditReply_ByOtherUser_IsRejected()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);

        var result = await Create(ctx).EditReplyAsync(stranger, replyId, "hacked", isAdmin: false);

        Assert.False(result.Succeeded);
        var stored = await ctx.ForumReplies.FirstAsync(r => r.Id == replyId);
        Assert.Equal("original", stored.Body);
        Assert.Null(stored.UpdatedAt);
    }

    [Fact]
    public async Task EditReply_ByAdmin_Succeeds()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var admin = await TestDb.AddUserAsync(ctx, "Admin");
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);

        var result = await Create(ctx).EditReplyAsync(admin, replyId, "moderated", isAdmin: true);

        Assert.True(result.Succeeded);
        Assert.Equal("moderated", result.Value!.Body);
    }

    [Fact]
    public async Task EditReply_MissingReply_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "User");

        var result = await Create(ctx).EditReplyAsync(user, Guid.NewGuid(), "body", isAdmin: false);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DeleteReply_ByAuthor_SoftDeletesAndHidesFromView()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var (topicId, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);

        var svc = Create(ctx);
        var result = await svc.DeleteReplyAsync(author, replyId, isAdmin: false);

        Assert.True(result.Succeeded);
        // The row is kept (to preserve threading) but flagged deleted.
        var stored = await ctx.ForumReplies.FirstAsync(r => r.Id == replyId);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.UpdatedAt);

        // Deleted replies are hidden entirely when the topic is read back.
        var topic = await svc.GetTopicAsync(topicId, author);
        Assert.Empty(topic.Value!.Replies);
    }

    [Fact]
    public async Task ToggleReplyReaction_AddsThenRemoves()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var reactor = await TestDb.AddUserAsync(ctx, "Reactor");
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);
        var svc = Create(ctx);

        // First toggle adds the reaction.
        var added = await svc.ToggleReplyReactionAsync(reactor, replyId, "👍");
        Assert.True(added.Succeeded);
        var like = Assert.Single(added.Value!);
        Assert.Equal("👍", like.Emoji);
        Assert.Equal(1, like.Count);
        Assert.True(like.Mine);

        // Second toggle of the same emoji removes it.
        var removed = await svc.ToggleReplyReactionAsync(reactor, replyId, "👍");
        Assert.True(removed.Succeeded);
        Assert.Empty(removed.Value!);
    }

    [Fact]
    public async Task ToggleReplyReaction_OnDeletedReply_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);
        var svc = Create(ctx);
        await svc.DeleteReplyAsync(author, replyId, isAdmin: false);

        var result = await svc.ToggleReplyReactionAsync(author, replyId, "👍");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task DeleteReply_ByOtherUser_IsRejected()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);

        var result = await Create(ctx).DeleteReplyAsync(stranger, replyId, isAdmin: false);

        Assert.False(result.Succeeded);
        Assert.False((await ctx.ForumReplies.FirstAsync(r => r.Id == replyId)).IsDeleted);
    }

    [Fact]
    public async Task DeleteReply_ClearsSolutionFlag()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);
        var reply = await ctx.ForumReplies.FirstAsync(r => r.Id == replyId);
        reply.IsSolution = true;
        await ctx.SaveChangesAsync();

        await Create(ctx).DeleteReplyAsync(author, replyId, isAdmin: true);

        var stored = await ctx.ForumReplies.FirstAsync(r => r.Id == replyId);
        Assert.True(stored.IsDeleted);
        Assert.False(stored.IsSolution);
    }

    [Fact]
    public async Task EditTopic_ByAuthor_UpdatesTitleAndBody()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var (topicId, _) = await SeedTopicWithReplyAsync(ctx, author, author);

        var result = await Create(ctx).EditTopicAsync(author, topicId, "  New title  ", "  new body  ", new List<string> { "csharp" }, isAdmin: false);

        Assert.True(result.Succeeded);
        Assert.Equal("New title", result.Value!.Title);
        Assert.Equal(new[] { "csharp" }, result.Value!.Tags);
        var stored = await ctx.ForumTopics.FirstAsync(t => t.Id == topicId);
        Assert.Equal("New title", stored.Title);
        Assert.Equal("new body", stored.Body);
        Assert.Equal(new[] { "csharp" }, stored.Tags);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task CreateTopic_NormalizesTags()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var dto = new Taskpilot.API.DTOs.Forum.CreateTopicDto
        {
            Title = "Tagged topic",
            Body = "body",
            // Blank, duplicate (case-insensitive) and over-cap entries.
            Tags = new List<string> { " EF ", "ef", "", "sql", "sql", "a", "b", "c" },
        };

        var result = await Create(ctx).CreateTopicAsync(author, dto);

        Assert.True(result.Succeeded);
        // "EF" kept once, blanks dropped, capped at 5.
        Assert.Equal(new[] { "EF", "sql", "a", "b", "c" }, result.Value!.Tags);
    }

    [Fact]
    public async Task GetTopics_TagFilter_ReturnsOnlyMatching()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var tagged = new ForumTopic { Id = Guid.NewGuid(), Title = "Tagged", Body = "b", AuthorId = author, CreatedAt = DateTime.UtcNow, Tags = new List<string> { "docker" } };
        var other = new ForumTopic { Id = Guid.NewGuid(), Title = "Other", Body = "b", AuthorId = author, CreatedAt = DateTime.UtcNow, Tags = new List<string> { "linux" } };
        ctx.ForumTopics.AddRange(tagged, other);
        await ctx.SaveChangesAsync();

        var result = await Create(ctx).GetTopicsAsync(tag: "docker");

        Assert.Contains(result.Value!.Items, t => t.Id == tagged.Id);
        Assert.DoesNotContain(result.Value!.Items, t => t.Id == other.Id);
    }

    [Fact]
    public async Task EditTopic_ByOtherUser_IsRejected()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var (topicId, _) = await SeedTopicWithReplyAsync(ctx, author, author);

        var result = await Create(ctx).EditTopicAsync(stranger, topicId, "Hacked", "hacked", new List<string>(), isAdmin: false);

        Assert.False(result.Succeeded);
        Assert.Equal("T", (await ctx.ForumTopics.FirstAsync(t => t.Id == topicId)).Title);
    }

    [Fact]
    public async Task SetTopicPinned_ByAdmin_Succeeds_ByUser_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var (topicId, _) = await SeedTopicWithReplyAsync(ctx, author, author);
        var svc = Create(ctx);

        Assert.False((await svc.SetTopicPinnedAsync(topicId, author, true, isAdmin: false)).Succeeded);
        Assert.True((await svc.SetTopicPinnedAsync(topicId, author, true, isAdmin: true)).Succeeded);
        Assert.True((await ctx.ForumTopics.FirstAsync(t => t.Id == topicId)).IsPinned);
    }

    [Fact]
    public async Task SetTopicLocked_ByAuthor_Succeeds_ByStranger_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var (topicId, _) = await SeedTopicWithReplyAsync(ctx, author, author);
        var svc = Create(ctx);

        Assert.False((await svc.SetTopicLockedAsync(topicId, stranger, true, isAdmin: false)).Succeeded);
        Assert.True((await svc.SetTopicLockedAsync(topicId, author, true, isAdmin: false)).Succeeded);
        Assert.True((await ctx.ForumTopics.FirstAsync(t => t.Id == topicId)).IsLocked);
    }

    [Fact]
    public async Task ToggleSubscription_AddsThenRemoves()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var follower = await TestDb.AddUserAsync(ctx, "Follower");
        var (topicId, _) = await SeedTopicWithReplyAsync(ctx, author, author);
        var svc = Create(ctx);

        var first = await svc.ToggleSubscriptionAsync(topicId, follower);
        Assert.True(first.Value);
        Assert.Equal(1, await ctx.ForumTopicSubscriptions.CountAsync());

        var second = await svc.ToggleSubscriptionAsync(topicId, follower);
        Assert.False(second.Value);
        Assert.Equal(0, await ctx.ForumTopicSubscriptions.CountAsync());
    }

    [Fact]
    public async Task GetTopics_SolvedFilter_ReturnsOnlyMatching()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        // Solved topic: a reply marked as solution.
        var (solvedId, solvedReply) = await SeedTopicWithReplyAsync(ctx, author, author);
        (await ctx.ForumReplies.FirstAsync(r => r.Id == solvedReply)).IsSolution = true;
        // Unsolved topic.
        var (unsolvedId, _) = await SeedTopicWithReplyAsync(ctx, author, author);
        await ctx.SaveChangesAsync();
        var svc = Create(ctx);

        var solved = await svc.GetTopicsAsync(solved: true);
        Assert.Contains(solved.Value!.Items, t => t.Id == solvedId);
        Assert.DoesNotContain(solved.Value!.Items, t => t.Id == unsolvedId);
        Assert.All(solved.Value!.Items, t => Assert.True(t.IsSolved));

        var unsolved = await svc.GetTopicsAsync(solved: false);
        Assert.Contains(unsolved.Value!.Items, t => t.Id == unsolvedId);
        Assert.DoesNotContain(unsolved.Value!.Items, t => t.Id == solvedId);
    }

    [Fact]
    public async Task ReportReply_CreatesPending_AndDedupesSameUser()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var reporter = await TestDb.AddUserAsync(ctx, "Reporter");
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);
        var svc = Create(ctx);

        Assert.True((await svc.ReportReplyAsync(reporter, replyId, "spam")).Succeeded);
        // A second report by the same user on the same reply is a no-op.
        Assert.True((await svc.ReportReplyAsync(reporter, replyId, "again")).Succeeded);
        Assert.Equal(1, await ctx.ForumReports.CountAsync());
        Assert.Equal(1, await svc.GetPendingReportCountAsync());
    }

    [Fact]
    public async Task GetReports_And_Resolve_MovesOutOfPending()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var reporter = await TestDb.AddUserAsync(ctx, "Reporter");
        var admin = await TestDb.AddUserAsync(ctx, "Admin");
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);
        var svc = Create(ctx);
        await svc.ReportReplyAsync(reporter, replyId, "bad");

        var pending = await svc.GetReportsAsync("Pending");
        var report = Assert.Single(pending.Value!);
        Assert.Equal("Reporter", report.ReporterName);
        Assert.Equal(replyId, report.ReplyId);

        Assert.True((await svc.ResolveReportAsync(admin, report.Id, dismiss: false)).Succeeded);
        Assert.Equal(0, await svc.GetPendingReportCountAsync());
        var stored = await ctx.ForumReports.FirstAsync();
        Assert.Equal(ForumReportStatus.Resolved, stored.Status);
        Assert.Equal(admin, stored.ResolvedById);
    }

    [Fact]
    public async Task ReportReply_OnDeletedReply_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var reporter = await TestDb.AddUserAsync(ctx, "Reporter");
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, author, author);
        var svc = Create(ctx);
        await svc.DeleteReplyAsync(author, replyId, isAdmin: false);

        Assert.False((await svc.ReportReplyAsync(reporter, replyId, null)).Succeeded);
    }

    [Fact]
    public async Task AddReply_NotifiesMentionedParticipant()
    {
        await using var ctx = TestDb.CreateContext();
        var alice = await TestDb.AddUserAsync(ctx, "Alice");
        var bob = await TestDb.AddUserAsync(ctx, "Bob");
        var carol = await TestDb.AddUserAsync(ctx, "Carol");
        // Topic by Alice with an existing reply by Bob (so Bob is a participant).
        var (topicId, _) = await SeedTopicWithReplyAsync(ctx, alice, bob);
        var (svc, notifications) = CreateWithMock(ctx);

        await svc.AddReplyAsync(carol, new Taskpilot.API.DTOs.Forum.CreateReplyDto { TopicId = topicId, Body = "Great point @Bob!" });

        notifications.Verify(n => n.CreateAsync(
            bob, It.IsAny<NotificationType>(), It.Is<string>(s => s.Contains("mentioned")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task MarkSolution_NotifiesReplyAuthor()
    {
        await using var ctx = TestDb.CreateContext();
        var topicAuthor = await TestDb.AddUserAsync(ctx, "TopicAuthor");
        var replyAuthor = await TestDb.AddUserAsync(ctx, "ReplyAuthor");
        // Topic by one user, the reply by another.
        var (_, replyId) = await SeedTopicWithReplyAsync(ctx, topicAuthor, replyAuthor);
        var (svc, notifications) = CreateWithMock(ctx);

        await svc.MarkSolutionAsync(topicAuthor, replyId);

        notifications.Verify(n => n.CreateAsync(
            replyAuthor, It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AddReply_NotifiesTopicSubscribers()
    {
        await using var ctx = TestDb.CreateContext();
        var author = await TestDb.AddUserAsync(ctx, "Author");
        var follower = await TestDb.AddUserAsync(ctx, "Follower");
        var replier = await TestDb.AddUserAsync(ctx, "Replier");
        var (topicId, _) = await SeedTopicWithReplyAsync(ctx, author, author);
        var (svc, notifications) = CreateWithMock(ctx);
        await svc.ToggleSubscriptionAsync(topicId, follower);

        await svc.AddReplyAsync(replier, new Taskpilot.API.DTOs.Forum.CreateReplyDto { TopicId = topicId, Body = "hi" });

        notifications.Verify(n => n.CreateAsync(
            follower, It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
