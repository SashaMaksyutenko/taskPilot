using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for achievement badges computed from task completions and the reputation ledger.</summary>
public class AchievementServiceTests
{
    private static AchievementService Make(TaskpilotDbContext ctx) => new(ctx);

    private static void AddRep(TaskpilotDbContext ctx, Guid userId, int delta, ReputationReason reason) =>
        ctx.ReputationEntries.Add(new ReputationEntry
        {
            Id = Guid.NewGuid(), UserId = userId, Delta = delta, Reason = reason, Description = "x", CreatedAt = DateTime.UtcNow,
        });

    private static void AddDoneTask(TaskpilotDbContext ctx, Guid projectId, Guid owner, Guid assignee) =>
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(), ProjectId = projectId, CreatorId = owner,
            Title = "T", Status = ProjectTaskStatus.Done, AssigneeId = assignee,
        });

    private static bool Earned(IEnumerable<Taskpilot.API.DTOs.Users.AchievementDto> badges, string code) =>
        badges.Single(b => b.Code == code).Earned;

    [Fact]
    public async Task NoActivity_NothingEarned()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");

        var badges = await Make(ctx).GetForUserAsync(user);

        Assert.NotEmpty(badges);
        Assert.All(badges, b => Assert.False(b.Earned));
    }

    [Fact]
    public async Task CompletingTasks_EarnsTaskBadges()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        var project = await TestDb.AddProjectAsync(ctx, user, "P");
        for (var i = 0; i < 10; i++) AddDoneTask(ctx, project, user, user);
        await ctx.SaveChangesAsync();

        var badges = await Make(ctx).GetForUserAsync(user);

        Assert.True(Earned(badges, "first_task"));
        Assert.True(Earned(badges, "ten_tasks"));
        Assert.False(Earned(badges, "fifty_tasks")); // needs 50
        Assert.Equal(10, badges.Single(b => b.Code == "ten_tasks").Current);
    }

    [Fact]
    public async Task ReputationEvents_EarnReputationBadges()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "U");
        for (var i = 0; i < 5; i++) AddRep(ctx, user, 10, ReputationReason.TaskOnTime); // 5 on-time, +50 score
        AddRep(ctx, user, 60, ReputationReason.TaskEarly);                               // score now 110
        AddRep(ctx, user, 15, ReputationReason.ForumSolution);
        AddRep(ctx, user, 10, ReputationReason.MarketplaceCompleted);
        await ctx.SaveChangesAsync();

        var badges = await Make(ctx).GetForUserAsync(user);

        Assert.True(Earned(badges, "punctual"));   // ≥5 on-time/early
        Assert.True(Earned(badges, "reputable"));  // score ≥100
        Assert.True(Earned(badges, "solver"));     // ≥1 accepted solution
        Assert.True(Earned(badges, "trader"));     // ≥1 marketplace task
    }
}
