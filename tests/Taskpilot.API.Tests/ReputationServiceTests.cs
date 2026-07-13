using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for the reputation ledger (<see cref="ReputationService"/>).</summary>
public class ReputationServiceTests
{
    private static ReputationService Create(TaskpilotDbContext ctx) =>
        new(ctx, NullLogger<ReputationService>.Instance);

    [Fact]
    public async Task Record_AppendsEntry_AndHistoryReturnsTotal()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var svc = Create(ctx);

        await svc.RecordAsync(user, 15, ReputationReason.ForumSolution, "Great answer");
        await svc.RecordAsync(user, -5, ReputationReason.WarningIssued, "Spam");

        var history = await svc.GetHistoryAsync(user);
        Assert.Equal(2, history.Entries.Count);
        Assert.Equal(10, history.LedgerTotal);
        // Newest first.
        Assert.Equal("WarningIssued", history.Entries[0].Reason);
    }

    [Fact]
    public async Task Record_Once_IsIdempotentPerEntity()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var entity = Guid.NewGuid();
        var svc = Create(ctx);

        await svc.RecordAsync(user, 10, ReputationReason.MarketplaceCompleted, "Job", entity, once: true);
        await svc.RecordAsync(user, 10, ReputationReason.MarketplaceCompleted, "Job", entity, once: true);

        Assert.Equal(1, await ctx.ReputationEntries.CountAsync());
    }

    [Fact]
    public async Task RecordTaskCompletion_Early_LogsFifteen()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var deadline = DateTime.UtcNow;
        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            Title = "Ship",
            AssigneeId = user,
            Deadline = deadline,
            CompletedAt = deadline.AddDays(-2), // 2 days early
            Status = ProjectTaskStatus.Done,
        };

        await Create(ctx).RecordTaskCompletionAsync(task);

        var entry = await ctx.ReputationEntries.SingleAsync();
        Assert.Equal(15, entry.Delta);
        Assert.Equal(ReputationReason.TaskEarly, entry.Reason);
    }

    [Fact]
    public async Task RecordTaskCompletion_Late_LogsMinusTen()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var deadline = DateTime.UtcNow.AddDays(-3);
        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            Title = "Ship",
            AssigneeId = user,
            Deadline = deadline,
            CompletedAt = DateTime.UtcNow, // 3 days late
            Status = ProjectTaskStatus.Done,
        };

        await Create(ctx).RecordTaskCompletionAsync(task);

        var entry = await ctx.ReputationEntries.SingleAsync();
        Assert.Equal(-10, entry.Delta);
        Assert.Equal(ReputationReason.TaskLate, entry.Reason);
    }

    [Fact]
    public async Task RecordTaskCompletion_NoAssigneeOrDeadline_Skips()
    {
        await using var ctx = TestDb.CreateContext();
        var svc = Create(ctx);

        await svc.RecordTaskCompletionAsync(new ProjectTask { Id = Guid.NewGuid(), Title = "X", Deadline = DateTime.UtcNow });
        await svc.RecordTaskCompletionAsync(new ProjectTask { Id = Guid.NewGuid(), Title = "X", AssigneeId = Guid.NewGuid() });

        Assert.Equal(0, await ctx.ReputationEntries.CountAsync());
    }

    [Fact]
    public async Task RecordTaskCompletion_Idempotent_PerTask()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var deadline = DateTime.UtcNow.AddDays(-1);
        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            Title = "Ship",
            AssigneeId = user,
            Deadline = deadline,
            CompletedAt = DateTime.UtcNow,
            Status = ProjectTaskStatus.Done,
        };
        var svc = Create(ctx);

        await svc.RecordTaskCompletionAsync(task);
        await svc.RecordTaskCompletionAsync(task); // re-completion must not double-log

        Assert.Equal(1, await ctx.ReputationEntries.CountAsync());
    }
}
