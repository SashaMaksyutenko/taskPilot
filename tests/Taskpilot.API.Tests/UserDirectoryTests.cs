using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Taskpilot.API.Data;
using Taskpilot.API.Models;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Tests the public users directory (feedback #4): active users only, sorted by name, paged.
/// (Name-filter search uses Postgres ILike and is covered by the live E2E, not here.)
/// </summary>
public class UserDirectoryTests
{
    private static UserService Create(TaskpilotDbContext ctx) =>
        new(ctx, Mock.Of<IFileService>(), NullLogger<UserService>.Instance);

    private static async Task AddUserAsync(TaskpilotDbContext ctx, string name, bool active = true)
    {
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = $"{name}@test.local",
            Role = Role.Developer,
            IsActive = active,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Directory_ListsActiveUsersSortedByName_ExcludingInactive()
    {
        await using var ctx = TestDb.CreateContext();
        await AddUserAsync(ctx, "Carol");
        await AddUserAsync(ctx, "Alice");
        await AddUserAsync(ctx, "Bob");
        await AddUserAsync(ctx, "Banned", active: false);
        var svc = Create(ctx);

        var result = await svc.GetUsersDirectoryAsync(page: 1, pageSize: 20, search: null);

        Assert.True(result.Succeeded);
        var page = result.Value!;
        Assert.Equal(3, page.Total); // the inactive user is excluded
        Assert.Equal(new[] { "Alice", "Bob", "Carol" }, page.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task Directory_Paginates()
    {
        await using var ctx = TestDb.CreateContext();
        foreach (var n in new[] { "Ann", "Bea", "Cid", "Dan", "Eve" })
            await AddUserAsync(ctx, n);
        var svc = Create(ctx);

        var first = (await svc.GetUsersDirectoryAsync(1, 2, null)).Value!;
        var second = (await svc.GetUsersDirectoryAsync(2, 2, null)).Value!;
        var third = (await svc.GetUsersDirectoryAsync(3, 2, null)).Value!;

        Assert.Equal(5, first.Total);
        Assert.Equal(new[] { "Ann", "Bea" }, first.Items.Select(i => i.Name).ToArray());
        Assert.Equal(new[] { "Cid", "Dan" }, second.Items.Select(i => i.Name).ToArray());
        Assert.Equal(new[] { "Eve" }, third.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task Directory_ClampsPageSize()
    {
        await using var ctx = TestDb.CreateContext();
        await AddUserAsync(ctx, "Solo");
        var svc = Create(ctx);

        // A page size over the cap (50) or below 1 is clamped.
        var big = (await svc.GetUsersDirectoryAsync(1, 999, null)).Value!;
        var zero = (await svc.GetUsersDirectoryAsync(1, 0, null)).Value!;

        Assert.Equal(50, big.PageSize);
        Assert.Equal(20, zero.PageSize); // 0 -> default 20
    }
}
