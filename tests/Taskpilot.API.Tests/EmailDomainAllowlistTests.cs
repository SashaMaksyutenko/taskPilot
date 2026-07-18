using Taskpilot.API.Common;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="EmailDomainAllowlist"/> — the registration gate. Getting a
/// boundary wrong either blocks everyone or lets any domain in, so the edges are pinned.
/// </summary>
public class EmailDomainAllowlistTests
{
    [Fact]
    public void EmptyConfig_IsOff_AndAllowsAnyDomain()
    {
        var list = EmailDomainAllowlist.Parse("");

        Assert.False(list.IsEnabled);
        Assert.True(list.IsAllowed("anyone@wherever.com"));
    }

    [Fact]
    public void AllowsOnlyListedDomains_CaseInsensitive()
    {
        var list = EmailDomainAllowlist.Parse("acme.com, acme.io");

        Assert.True(list.IsEnabled);
        Assert.True(list.IsAllowed("alice@acme.com"));
        Assert.True(list.IsAllowed("BOB@ACME.IO"));       // case-insensitive
        Assert.False(list.IsAllowed("mallory@evil.com")); // not listed
    }

    [Fact]
    public void StripsLeadingAt_AndIgnoresBlanks()
    {
        var list = EmailDomainAllowlist.Parse("@acme.com, , ,  @acme.io  ");

        Assert.Equal(2, list.Domains.Count);
        Assert.True(list.IsAllowed("x@acme.com"));
        Assert.True(list.IsAllowed("y@acme.io"));
    }

    [Fact]
    public void MatchesTheDomainAfterTheLastAt()
    {
        var list = EmailDomainAllowlist.Parse("acme.com");

        // A '+' or extra '@' in the local part must not fool the domain check.
        Assert.True(list.IsAllowed("weird@name@acme.com"));
        Assert.False(list.IsAllowed("weird@acme.com@evil.com"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("no-domain")]
    [InlineData("trailing@")]
    public void MalformedEmails_AreRejectedWhenTheListIsOn(string? email)
    {
        var list = EmailDomainAllowlist.Parse("acme.com");
        Assert.False(list.IsAllowed(email));
    }

    [Fact]
    public void Domains_AreDeduplicatedAndLowerCased()
    {
        var list = EmailDomainAllowlist.Parse("Acme.com, ACME.COM, acme.com");
        Assert.Single(list.Domains);
        Assert.Contains("acme.com", list.Domains);
    }
}
