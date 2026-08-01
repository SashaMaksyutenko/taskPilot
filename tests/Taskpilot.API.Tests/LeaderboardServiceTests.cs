using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Tests for the reputation leaderboard: ranking, positive-only filter, ties and self standing.</summary>
public class LeaderboardServiceTests
{
    private static LeaderboardService Make(TaskpilotDbContext ctx) => new(ctx);

    private static void AddRep(TaskpilotDbContext ctx, Guid userId, int delta) =>
        ctx.ReputationEntries.Add(new ReputationEntry
        {
            Id = Guid.NewGuid(), UserId = userId, Delta = delta,
            Reason = ReputationReason.TaskOnTime, Description = "x", CreatedAt = DateTime.UtcNow,
        });

    private static void AddDoneTask(TaskpilotDbContext ctx, Guid projectId, Guid creator, Guid assignee) =>
        ctx.ProjectTasks.Add(new ProjectTask
        {
            Id = Guid.NewGuid(), ProjectId = projectId, CreatorId = creator,
            Title = "T", Status = ProjectTaskStatus.Done, AssigneeId = assignee,
        });

    [Fact]
    public async Task RanksUsersByScoreDescending()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var c = await TestDb.AddUserAsync(ctx, "C");
        AddRep(ctx, a, 30);
        AddRep(ctx, b, 10);
        AddRep(ctx, c, 20);
        await ctx.SaveChangesAsync();

        var board = await Make(ctx).GetAsync(a);

        Assert.Equal(new[] { a, c, b }, board.Entries.Select(e => e.UserId).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, board.Entries.Select(e => e.Rank).ToArray());
        Assert.Equal(30, board.Entries[0].Score);
    }

    [Fact]
    public async Task ExcludesUsersWithNonPositiveScore()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var d = await TestDb.AddUserAsync(ctx, "D");
        AddRep(ctx, a, 10);
        AddRep(ctx, d, 5);
        AddRep(ctx, d, -10); // net −5
        await ctx.SaveChangesAsync();

        var board = await Make(ctx).GetAsync(a);

        Assert.Single(board.Entries);
        Assert.Equal(a, board.Entries[0].UserId);
    }

    [Fact]
    public async Task Me_ShowsCallerRank_EvenOutsideTopN()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var c = await TestDb.AddUserAsync(ctx, "C");
        AddRep(ctx, a, 30);
        AddRep(ctx, b, 20);
        AddRep(ctx, c, 10);
        await ctx.SaveChangesAsync();

        var board = await Make(ctx).GetAsync(c, limit: 1);

        Assert.Single(board.Entries); // only the top user is returned
        Assert.Equal(a, board.Entries[0].UserId);
        Assert.NotNull(board.Me);
        Assert.Equal(3, board.Me!.Rank);
        Assert.Equal(10, board.Me.Score);
    }

    [Fact]
    public async Task Ties_BrokenByTasksCompleted()
    {
        await using var ctx = TestDb.CreateContext();
        var a = await TestDb.AddUserAsync(ctx, "A");
        var b = await TestDb.AddUserAsync(ctx, "B");
        var project = await TestDb.AddProjectAsync(ctx, a, "P");
        AddRep(ctx, a, 20);
        AddRep(ctx, b, 20);
        // B has finished two tasks; A none.
        AddDoneTask(ctx, project, a, b);
        AddDoneTask(ctx, project, a, b);
        await ctx.SaveChangesAsync();

        var board = await Make(ctx).GetAsync(a);

        Assert.Equal(b, board.Entries[0].UserId); // same score, more done → higher
        Assert.Equal(2, board.Entries[0].TasksCompleted);
        Assert.Equal(a, board.Entries[1].UserId);
        Assert.Equal(0, board.Entries[1].TasksCompleted);
    }
}
