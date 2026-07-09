using Taskpilot.API.Hubs;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>Unit tests for <see cref="PresenceTracker"/>, including the disconnect grace period.</summary>
public class PresenceTrackerTests
{
    [Fact]
    public void Connected_MakesUserOnline()
    {
        var tracker = new PresenceTracker();
        var user = Guid.NewGuid();

        tracker.Connected(user, "conn-1");

        Assert.True(tracker.IsOnline(user));
        Assert.Contains(user, tracker.OnlineUserIds());
        Assert.Equal(1, tracker.OnlineCount);
    }

    [Fact]
    public void Disconnect_OfLastConnection_KeepsUserOnlineDuringGrace()
    {
        var tracker = new PresenceTracker();
        var user = Guid.NewGuid();
        tracker.Connected(user, "conn-1");

        tracker.Disconnected(user, "conn-1");

        // The grace period keeps a briefly-dropped user online (no flicker).
        Assert.True(tracker.IsOnline(user));
        Assert.Contains(user, tracker.OnlineUserIds());
    }

    [Fact]
    public void Reconnect_WithinGrace_StaysOnlineViaLiveConnection()
    {
        var tracker = new PresenceTracker();
        var user = Guid.NewGuid();
        tracker.Connected(user, "conn-1");
        tracker.Disconnected(user, "conn-1"); // now in grace
        tracker.Connected(user, "conn-2");    // quick auto-reconnect

        Assert.True(tracker.IsOnline(user));
        Assert.Single(tracker.OnlineUserIds());
    }

    [Fact]
    public void MultipleConnections_StayOnlineUntilLastDrops()
    {
        var tracker = new PresenceTracker();
        var user = Guid.NewGuid();
        tracker.Connected(user, "tab-1");
        tracker.Connected(user, "tab-2");

        tracker.Disconnected(user, "tab-1");

        // Still has a live connection (tab-2).
        Assert.True(tracker.IsOnline(user));
    }

    [Fact]
    public void SeparateUsers_AreCountedIndependently()
    {
        var tracker = new PresenceTracker();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        tracker.Connected(a, "a1");
        tracker.Connected(b, "b1");

        Assert.Equal(2, tracker.OnlineUserIds().Count);
    }
}
