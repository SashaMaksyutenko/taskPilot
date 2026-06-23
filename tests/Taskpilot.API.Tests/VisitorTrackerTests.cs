using Taskpilot.API.Services;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for the in-memory <see cref="VisitorTracker"/>.</summary>
public class VisitorTrackerTests
{
    [Fact]
    public void Record_CountsTotalVisitsAndUniqueIps()
    {
        var tracker = new VisitorTracker();

        tracker.Record("1.1.1.1");
        tracker.Record("1.1.1.1"); // same IP — counts as a visit but not a new unique
        tracker.Record("2.2.2.2");

        Assert.Equal(3, tracker.TotalVisits);          // every request counted
        Assert.Equal(2, tracker.UniqueVisitorsToday);  // two distinct IPs
    }

    [Fact]
    public void Record_NullIp_CountsVisitButNotUnique()
    {
        var tracker = new VisitorTracker();

        tracker.Record(null);
        tracker.Record("");

        Assert.Equal(2, tracker.TotalVisits);          // both counted as visits
        Assert.Equal(0, tracker.UniqueVisitorsToday);  // no usable IP recorded
    }

    [Fact]
    public void NewTracker_StartsEmpty()
    {
        var tracker = new VisitorTracker();

        Assert.Equal(0, tracker.TotalVisits);
        Assert.Equal(0, tracker.UniqueVisitorsToday);
    }
}
