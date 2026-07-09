using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="VisitorService"/> using the in-memory EF provider.</summary>
public class VisitorServiceTests
{
    [Fact]
    public async Task Record_SameIpTwice_CountsOneUniqueVisitorAndTwoHits()
    {
        await using var ctx = TestDb.CreateContext();
        var service = new VisitorService(ctx);

        await service.RecordAsync("203.0.113.5");
        await service.RecordAsync("203.0.113.5");

        Assert.Equal(1, await service.UniqueVisitorsTodayAsync());
        Assert.Equal(2, await service.TotalVisitsAsync());
    }

    [Fact]
    public async Task Record_DifferentIps_CountsDistinctVisitors()
    {
        await using var ctx = TestDb.CreateContext();
        var service = new VisitorService(ctx);

        await service.RecordAsync("203.0.113.5");
        await service.RecordAsync("198.51.100.9");

        Assert.Equal(2, await service.UniqueVisitorsTodayAsync());
        Assert.Equal(2, await service.TotalVisitsAsync());
    }

    [Fact]
    public async Task Record_StoresHashNotRawIp()
    {
        await using var ctx = TestDb.CreateContext();
        var service = new VisitorService(ctx);

        await service.RecordAsync("203.0.113.5");

        var row = await ctx.VisitorHits.SingleAsync();
        Assert.DoesNotContain("203.0.113.5", row.IpHash);
        Assert.Equal(64, row.IpHash.Length); // SHA-256 hex
    }

    [Fact]
    public async Task Totals_AreZero_WhenNoVisits()
    {
        await using var ctx = TestDb.CreateContext();
        var service = new VisitorService(ctx);

        Assert.Equal(0, await service.UniqueVisitorsTodayAsync());
        Assert.Equal(0, await service.TotalVisitsAsync());
    }
}
