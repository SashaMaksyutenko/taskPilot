using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Search;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="SavedSearchService"/>.</summary>
public class SavedSearchServiceTests
{
    private static SavedSearchService Create(TaskpilotDbContext ctx) =>
        new(ctx, NullLogger<SavedSearchService>.Instance);

    private static CreateSavedSearchDto Dto(string name = "My tasks", string query = "urgent") =>
        new() { Name = name, Query = query };

    [Fact]
    public async Task Create_ThenListReturnsIt()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var svc = Create(ctx);

        var created = await svc.CreateAsync(user, Dto());
        Assert.True(created.Succeeded);

        var mine = await svc.GetMineAsync(user);
        Assert.Single(mine.Value!);
        Assert.Equal("urgent", mine.Value![0].Query);
    }

    [Fact]
    public async Task Create_BlankFields_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");

        var result = await Create(ctx).CreateAsync(user, new CreateSavedSearchDto { Name = "  ", Query = "" });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GetMine_OnlyOwnSearches()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var svc = Create(ctx);
        await svc.CreateAsync(me, Dto("A", "a"));
        await svc.CreateAsync(other, Dto("B", "b"));

        var mine = await svc.GetMineAsync(me);
        Assert.Single(mine.Value!);
        Assert.Equal("A", mine.Value![0].Name);
    }

    [Fact]
    public async Task Delete_OnlyOwner()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var svc = Create(ctx);
        var created = (await svc.CreateAsync(me, Dto())).Value!;

        // A different user cannot delete it.
        Assert.False((await svc.DeleteAsync(other, created.Id)).Succeeded);
        // The owner can.
        Assert.True((await svc.DeleteAsync(me, created.Id)).Succeeded);
        Assert.Equal(0, await ctx.SavedSearches.CountAsync());
    }

    [Fact]
    public async Task Create_CappedPerUser()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "Dev");
        var svc = Create(ctx);
        for (var i = 0; i < 20; i++)
            Assert.True((await svc.CreateAsync(user, Dto($"S{i}", $"q{i}"))).Succeeded);

        // The 21st save is refused.
        Assert.False((await svc.CreateAsync(user, Dto("overflow", "x"))).Succeeded);
    }
}
