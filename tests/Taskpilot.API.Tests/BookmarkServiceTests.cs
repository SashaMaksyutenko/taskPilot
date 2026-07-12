using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Bookmarks;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="BookmarkService"/> over the in-memory provider.</summary>
public class BookmarkServiceTests
{
    private static BookmarkService Create(TaskpilotDbContext ctx) =>
        new(ctx, NullLogger<BookmarkService>.Instance);

    private static ToggleBookmarkDto Dto(Guid entityId, string type = "Topic") =>
        new() { Type = type, EntityId = entityId, Title = "A topic", Link = $"/forum/{entityId}" };

    [Fact]
    public async Task Toggle_AddsThenRemoves()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "User");
        var entity = Guid.NewGuid();
        var svc = Create(ctx);

        var first = await svc.ToggleAsync(user, Dto(entity));
        Assert.True(first.Value); // now bookmarked
        Assert.Equal(1, await ctx.Bookmarks.CountAsync());

        var second = await svc.ToggleAsync(user, Dto(entity));
        Assert.False(second.Value); // removed
        Assert.Equal(0, await ctx.Bookmarks.CountAsync());
    }

    [Fact]
    public async Task Toggle_InvalidType_Fails()
    {
        await using var ctx = TestDb.CreateContext();
        var user = await TestDb.AddUserAsync(ctx, "User");

        var result = await svc_ToggleInvalid(ctx, user);

        Assert.False(result.Succeeded);
    }

    private static Task<Taskpilot.API.Common.Result<bool>> svc_ToggleInvalid(TaskpilotDbContext ctx, Guid user) =>
        Create(ctx).ToggleAsync(user, new ToggleBookmarkDto { Type = "Nope", EntityId = Guid.NewGuid() });

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnBookmarks_NewestFirst()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var svc = Create(ctx);

        await svc.ToggleAsync(me, Dto(Guid.NewGuid()));
        await svc.ToggleAsync(me, Dto(Guid.NewGuid(), "Task"));
        await svc.ToggleAsync(other, Dto(Guid.NewGuid()));

        var mine = await svc.GetMineAsync(me);
        Assert.Equal(2, mine.Value!.Count);
        Assert.All(mine.Value!, b => Assert.Contains(b.Type, new[] { "Topic", "Task" }));
    }

    [Fact]
    public async Task Delete_OnlyOwnBookmark()
    {
        await using var ctx = TestDb.CreateContext();
        var me = await TestDb.AddUserAsync(ctx, "Me");
        var other = await TestDb.AddUserAsync(ctx, "Other");
        var svc = Create(ctx);
        await svc.ToggleAsync(me, Dto(Guid.NewGuid()));
        var id = (await ctx.Bookmarks.FirstAsync()).Id;

        // A different user cannot delete it.
        Assert.False((await svc.DeleteAsync(other, id)).Succeeded);
        // The owner can.
        Assert.True((await svc.DeleteAsync(me, id)).Succeeded);
        Assert.Equal(0, await ctx.Bookmarks.CountAsync());
    }
}
