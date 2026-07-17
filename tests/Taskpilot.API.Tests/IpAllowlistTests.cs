using System.Net;
using Taskpilot.API.Common;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="IpAllowlist"/> — the admin API's network gate. Getting a
/// boundary wrong here either locks admins out or lets everyone in, so the CIDR edges,
/// address families and malformed config are all pinned down.
/// </summary>
public class IpAllowlistTests
{
    private static IPAddress Ip(string s) => IPAddress.Parse(s);

    [Fact]
    public void EmptyConfig_IsOff_AndAllowsEverything()
    {
        var list = IpAllowlist.Parse("");

        Assert.False(list.IsEnabled);
        Assert.True(list.IsAllowed(Ip("8.8.8.8")));
        Assert.True(list.IsAllowed(null));
    }

    [Fact]
    public void SingleIp_MatchesOnlyThatExactAddress()
    {
        var list = IpAllowlist.Parse("203.0.113.5");

        Assert.True(list.IsEnabled);
        Assert.True(list.IsAllowed(Ip("203.0.113.5")));
        Assert.False(list.IsAllowed(Ip("203.0.113.6")));
    }

    [Theory]
    [InlineData("10.0.0.0", true)]      // network address
    [InlineData("10.1.2.3", true)]      // inside
    [InlineData("10.255.255.255", true)] // broadcast end of the range
    [InlineData("11.0.0.1", false)]     // just outside
    [InlineData("9.255.255.255", false)]
    public void Cidr_MatchesTheWholeRangeAndNothingElse(string ip, bool allowed)
    {
        var list = IpAllowlist.Parse("10.0.0.0/8");
        Assert.Equal(allowed, list.IsAllowed(Ip(ip)));
    }

    [Theory]
    [InlineData("192.168.1.0", true)]
    [InlineData("192.168.1.127", true)]  // last address of /25
    [InlineData("192.168.1.128", false)] // first address outside /25 — the bit-level edge
    public void Cidr_HandlesAPrefixThatIsNotAWholeNumberOfBytes(string ip, bool allowed)
    {
        var list = IpAllowlist.Parse("192.168.1.0/25");
        Assert.Equal(allowed, list.IsAllowed(Ip(ip)));
    }

    [Fact]
    public void IPv4MappedIPv6_IsMatchedAgainstIPv4Entries()
    {
        // Kestrel reports IPv4 callers like this; without normalising, "127.0.0.1" in
        // config would never match a real local request.
        var list = IpAllowlist.Parse("127.0.0.1");
        Assert.True(list.IsAllowed(Ip("::ffff:127.0.0.1")));
    }

    [Fact]
    public void IPv6Loopback_IsItsOwnAddress_AndNeedsListingSeparately()
    {
        // Verified live: a local browser connects over IPv6, so the server sees ::1 — NOT
        // 127.0.0.1 and NOT ::ffff:127.0.0.1. Listing only "127.0.0.1" locks the admin out.
        // Pinning this so the behaviour stays deliberate and documented in .env.example.
        var ipv4Only = IpAllowlist.Parse("127.0.0.1");
        Assert.False(ipv4Only.IsAllowed(Ip("::1")));

        var bothForms = IpAllowlist.Parse("127.0.0.1, ::1");
        Assert.True(bothForms.IsAllowed(Ip("::1")));
        Assert.True(bothForms.IsAllowed(Ip("127.0.0.1")));
    }

    [Fact]
    public void IPv6_IsSupported_AndDoesNotCrossMatchIPv4()
    {
        var list = IpAllowlist.Parse("2001:db8::/32");

        Assert.True(list.IsAllowed(Ip("2001:db8::1")));
        Assert.False(list.IsAllowed(Ip("2001:dba::1")));
        Assert.False(list.IsAllowed(Ip("10.0.0.1"))); // an IPv6 entry must not match IPv4
    }

    [Fact]
    public void MultipleEntries_AreAllHonoured_AndWhitespaceIsTolerated()
    {
        var list = IpAllowlist.Parse(" 203.0.113.5 , 10.0.0.0/8 ,2001:db8::/32 ");

        Assert.True(list.IsAllowed(Ip("203.0.113.5")));
        Assert.True(list.IsAllowed(Ip("10.9.9.9")));
        Assert.True(list.IsAllowed(Ip("2001:db8::99")));
        Assert.False(list.IsAllowed(Ip("8.8.8.8")));
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("10.0.0.0/99")]  // prefix out of range
    [InlineData("10.0.0.0/abc")] // prefix not a number
    [InlineData("   ")]
    public void MalformedEntries_AreSkippedRatherThanThrowing(string bad)
    {
        // A typo must not take the API down at startup, and must not silently
        // turn into an allow-all either.
        var onlyBad = IpAllowlist.Parse(bad);
        Assert.False(onlyBad.IsEnabled);

        var mixed = IpAllowlist.Parse($"{bad},203.0.113.5");
        Assert.True(mixed.IsEnabled);
        Assert.True(mixed.IsAllowed(Ip("203.0.113.5")));
        Assert.False(mixed.IsAllowed(Ip("8.8.8.8")));
    }

    [Fact]
    public void EnabledList_RejectsAnUnknownCaller()
    {
        var list = IpAllowlist.Parse("10.0.0.0/8");
        Assert.False(list.IsAllowed(null)); // no IP at all is not on the list
    }
}
