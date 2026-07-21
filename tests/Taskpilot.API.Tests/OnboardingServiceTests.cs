using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests for <see cref="OnboardingService"/> — the starter project a new account gets so
/// its first screen is a working board rather than an empty dashboard.
/// </summary>
public class OnboardingServiceTests
{
    private static OnboardingService Create(TaskpilotDbContext ctx, bool enabled = true) =>
        new(ctx,
            Options.Create(new OnboardingOptions { CreateSampleProject = enabled }),
            NullLogger<OnboardingService>.Instance);

    [Fact]
    public async Task CreatesAProjectOwnedByTheNewUser()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx);

        await Create(ctx).CreateStarterProjectAsync(userId);

        var project = await ctx.Projects.SingleAsync();
        Assert.Equal(userId, project.OwnerId);
        Assert.False(string.IsNullOrWhiteSpace(project.Name));
    }

    [Fact]
    public async Task FillsEveryKanbanColumn_SoTheBoardIsNeverEmpty()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx);

        await Create(ctx).CreateStarterProjectAsync(userId);

        var statuses = await ctx.ProjectTasks.Select(t => t.Status).ToListAsync();
        Assert.Contains(ProjectTaskStatus.Backlog, statuses);
        Assert.Contains(ProjectTaskStatus.InProgress, statuses);
        Assert.Contains(ProjectTaskStatus.Review, statuses);
        Assert.Contains(ProjectTaskStatus.Done, statuses);
    }

    [Fact]
    public async Task EveryTaskIsAssignedAndHasADeadline_SoCalendarAndCountersArePopulated()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx);

        await Create(ctx).CreateStarterProjectAsync(userId);

        var tasks = await ctx.ProjectTasks.ToListAsync();
        Assert.NotEmpty(tasks);
        Assert.All(tasks, t => Assert.Equal(userId, t.AssigneeId));
        Assert.All(tasks, t => Assert.NotNull(t.Deadline));
        // The finished one is dated in the past, like a real completed task.
        var done = tasks.Single(t => t.Status == ProjectTaskStatus.Done);
        Assert.NotNull(done.CompletedAt);
        Assert.True(done.Deadline < DateTime.UtcNow);
    }

    [Fact]
    public async Task NoTaskIsOverdue_SoANewAccountDoesNotOpenOntoWarnings()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx);

        await Create(ctx).CreateStarterProjectAsync(userId);

        // "Overdue" means past deadline AND not Done — a fresh account should have none.
        var overdue = await ctx.ProjectTasks
            .Where(t => t.Status != ProjectTaskStatus.Done && t.Deadline < DateTime.UtcNow)
            .CountAsync();
        Assert.Equal(0, overdue);
    }

    [Fact]
    public async Task CreatesNothing_WhenTheOptionIsOff()
    {
        await using var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx);

        await Create(ctx, enabled: false).CreateStarterProjectAsync(userId);

        Assert.False(await ctx.Projects.AnyAsync());
        Assert.False(await ctx.ProjectTasks.AnyAsync());
    }

    [Fact]
    public async Task NeverThrows_SoAFailureCannotBreakRegistration()
    {
        // A disposed context makes every database call fail.
        var ctx = TestDb.CreateContext();
        var userId = await TestDb.AddUserAsync(ctx);
        var service = Create(ctx);
        await ctx.DisposeAsync();

        // Registration has already succeeded by this point; this must swallow the error.
        await service.CreateStarterProjectAsync(userId);
    }
}
