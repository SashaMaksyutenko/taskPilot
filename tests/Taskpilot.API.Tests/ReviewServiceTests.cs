using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Reviews;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Taskpilot.Contracts;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for cross-context peer reviews: leaving a project review (eligibility + one-per-pair)
/// and reading a user's received reviews across every context with the context label resolved.
/// </summary>
public class ReviewServiceTests
{
    private static ReviewService Make(TaskpilotDbContext ctx, Mock<INotificationService>? notifications = null)
    {
        var notif = notifications ?? new Mock<INotificationService>();
        notif.Setup(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        return new ReviewService(ctx, notif.Object, NullLogger<ReviewService>.Instance);
    }

    private static async Task AddMemberAsync(TaskpilotDbContext ctx, Guid projectId, Guid userId)
    {
        ctx.ProjectMembers.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task LeaveProjectReview_MemberAboutMember_PersistsWithProjectContext()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var project = await TestDb.AddProjectAsync(ctx, owner, "Nebula");
        await AddMemberAsync(ctx, project, member);

        var svc = Make(ctx);
        var result = await svc.LeaveProjectReviewAsync(owner, project, new LeaveReviewDto { RateeId = member, Stars = 5, Comment = "Great teammate" });

        Assert.True(result.Succeeded);
        Assert.Equal("Project", result.Value!.Context);
        Assert.Equal("Nebula", result.Value.ContextLabel);
        var saved = Assert.Single(ctx.Reviews);
        Assert.Equal(ReviewContext.Project, saved.Context);
        Assert.Equal(project, saved.ContextId);
        Assert.Null(saved.MarketplaceTaskId);
        Assert.Equal(member, saved.RateeId);
    }

    [Fact]
    public async Task LeaveProjectReview_SelfReview_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var project = await TestDb.AddProjectAsync(ctx, owner, "Nebula");

        var svc = Make(ctx);
        var result = await svc.LeaveProjectReviewAsync(owner, project, new LeaveReviewDto { RateeId = owner, Stars = 4 });

        Assert.False(result.Succeeded);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task LeaveProjectReview_RateeNotAMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var stranger = await TestDb.AddUserAsync(ctx, "Stranger");
        var project = await TestDb.AddProjectAsync(ctx, owner, "Nebula");

        var svc = Make(ctx);
        var result = await svc.LeaveProjectReviewAsync(owner, project, new LeaveReviewDto { RateeId = stranger, Stars = 4 });

        Assert.False(result.Succeeded);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task LeaveProjectReview_RaterNotAMember_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var outsider = await TestDb.AddUserAsync(ctx, "Outsider");
        var project = await TestDb.AddProjectAsync(ctx, owner, "Nebula");
        await AddMemberAsync(ctx, project, member);

        var svc = Make(ctx);
        var result = await svc.LeaveProjectReviewAsync(outsider, project, new LeaveReviewDto { RateeId = member, Stars = 4 });

        Assert.False(result.Succeeded);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task LeaveProjectReview_Duplicate_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var project = await TestDb.AddProjectAsync(ctx, owner, "Nebula");
        await AddMemberAsync(ctx, project, member);

        var svc = Make(ctx);
        Assert.True((await svc.LeaveProjectReviewAsync(owner, project, new LeaveReviewDto { RateeId = member, Stars = 5 })).Succeeded);
        var second = await svc.LeaveProjectReviewAsync(owner, project, new LeaveReviewDto { RateeId = member, Stars = 3 });

        Assert.False(second.Succeeded);
        Assert.Single(ctx.Reviews);
    }

    [Fact]
    public async Task LeaveProjectReview_InvalidStars_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var member = await TestDb.AddUserAsync(ctx, "Member");
        var project = await TestDb.AddProjectAsync(ctx, owner, "Nebula");
        await AddMemberAsync(ctx, project, member);

        var svc = Make(ctx);
        var result = await svc.LeaveProjectReviewAsync(owner, project, new LeaveReviewDto { RateeId = member, Stars = 6 });

        Assert.False(result.Succeeded);
        Assert.Empty(ctx.Reviews);
    }

    [Fact]
    public async Task GetUserReviews_ReturnsAcrossContexts_WithResolvedLabels()
    {
        await using var ctx = TestDb.CreateContext();
        var ratee = await TestDb.AddUserAsync(ctx, "Ratee");
        var owner = await TestDb.AddUserAsync(ctx, "Owner");
        var poster = await TestDb.AddUserAsync(ctx, "Poster");
        var project = await TestDb.AddProjectAsync(ctx, owner, "Nebula");
        var gigId = Guid.NewGuid();
        ctx.MarketplaceTasks.Add(new MarketplaceTask
        {
            Id = gigId, Title = "Build a landing page", Description = "d", Budget = 100m, PosterId = poster,
        });
        ctx.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(), Context = ReviewContext.Project, ContextId = project, RaterId = owner, RateeId = ratee, Stars = 5,
        });
        ctx.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(), Context = ReviewContext.Marketplace, ContextId = gigId, MarketplaceTaskId = gigId,
            RaterId = poster, RateeId = ratee, Stars = 4,
        });
        await ctx.SaveChangesAsync();

        var svc = Make(ctx);
        var result = await svc.GetUserReviewsAsync(ratee);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Count);
        var project_ = Assert.Single(result.Value, r => r.Context == "Project");
        Assert.Equal("Nebula", project_.ContextLabel);
        Assert.Equal($"/projects/{project}", project_.ContextLink);
        var market = Assert.Single(result.Value, r => r.Context == "Marketplace");
        Assert.Equal("Build a landing page", market.ContextLabel);
    }
}
