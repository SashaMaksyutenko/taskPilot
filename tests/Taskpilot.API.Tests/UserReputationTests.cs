using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for the derived reputation score, focused on the late-task penalty
/// (1d=−2, 3d=−5, 5d+=−10) applied on top of the positive components.
/// </summary>
public class UserReputationTests
{
    private static UserService Create(TaskpilotDbContext ctx) =>
        new(ctx, new Mock<IFileService>().Object, NullLogger<UserService>.Instance);

    /// <summary>Gives the user positive base reputation via N completed marketplace tasks (10 pts each).</summary>
    private static async Task AddCompletedMarketplaceTasksAsync(TaskpilotDbContext ctx, Guid userId, Guid posterId, int count)
    {
        for (var i = 0; i < count; i++)
            ctx.MarketplaceTasks.Add(new MarketplaceTask
            {
                Id = Guid.NewGuid(),
                Title = $"Job {i}",
                Description = "…",
                Budget = 50m,
                Status = MarketplaceTaskStatus.Completed,
                PosterId = posterId,
                AssigneeId = userId,
            });
        await ctx.SaveChangesAsync();
    }

    private static async Task AddProjectTaskAsync(
        TaskpilotDbContext ctx, Guid projectId, Guid assigneeId,
        ProjectTaskStatus status, DateTime deadline, DateTime? completedAt)
    {
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "T",
            Status = status,
            AssigneeId = assigneeId,
            CreatorId = assigneeId,
            Deadline = deadline,
            CompletedAt = completedAt,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task NoLateTasks_FullPoints()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var poster = await TestDb.AddUserAsync(ctx, "Poster");
        await AddCompletedMarketplaceTasksAsync(ctx, user, poster, 3); // 30 points

        var profile = await Create(ctx).GetPublicProfileAsync(user);

        Assert.Equal(30, profile.Value!.ReputationPoints);
    }

    [Fact]
    public async Task DoneLate_FiveDays_SubtractsTen()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var poster = await TestDb.AddUserAsync(ctx, "Poster");
        var project = await TestDb.AddProjectAsync(ctx, poster);
        await AddCompletedMarketplaceTasksAsync(ctx, user, poster, 3); // 30
        // Finished 6 days after the deadline → −10.
        var deadline = DateTime.UtcNow.AddDays(-10);
        await AddProjectTaskAsync(ctx, project, user, ProjectTaskStatus.Done, deadline, deadline.AddDays(6));

        var profile = await Create(ctx).GetPublicProfileAsync(user);

        Assert.Equal(20, profile.Value!.ReputationPoints);
    }

    [Fact]
    public async Task StillOverdue_FourDays_SubtractsFive()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var poster = await TestDb.AddUserAsync(ctx, "Poster");
        var project = await TestDb.AddProjectAsync(ctx, poster);
        await AddCompletedMarketplaceTasksAsync(ctx, user, poster, 3); // 30
        // Not done, 4 days past the deadline → −5.
        await AddProjectTaskAsync(ctx, project, user, ProjectTaskStatus.InProgress, DateTime.UtcNow.AddDays(-4), null);

        var profile = await Create(ctx).GetPublicProfileAsync(user);

        Assert.Equal(25, profile.Value!.ReputationPoints);
    }

    [Fact]
    public async Task LessThanOneDayLate_NoPenalty()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var poster = await TestDb.AddUserAsync(ctx, "Poster");
        var project = await TestDb.AddProjectAsync(ctx, poster);
        await AddCompletedMarketplaceTasksAsync(ctx, user, poster, 3); // 30
        // Finished 12 hours after the deadline → no penalty (tier starts at 1 day).
        var deadline = DateTime.UtcNow.AddDays(-2);
        await AddProjectTaskAsync(ctx, project, user, ProjectTaskStatus.Done, deadline, deadline.AddHours(12));

        var profile = await Create(ctx).GetPublicProfileAsync(user);

        Assert.Equal(30, profile.Value!.ReputationPoints);
    }

    [Fact]
    public async Task OnTimeCompletion_NoPenalty()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var poster = await TestDb.AddUserAsync(ctx, "Poster");
        var project = await TestDb.AddProjectAsync(ctx, poster);
        await AddCompletedMarketplaceTasksAsync(ctx, user, poster, 3); // 30
        // Finished a day BEFORE the deadline → no penalty.
        var deadline = DateTime.UtcNow.AddDays(-2);
        await AddProjectTaskAsync(ctx, project, user, ProjectTaskStatus.Done, deadline, deadline.AddDays(-1));

        var profile = await Create(ctx).GetPublicProfileAsync(user);

        Assert.Equal(30, profile.Value!.ReputationPoints);
    }
}
