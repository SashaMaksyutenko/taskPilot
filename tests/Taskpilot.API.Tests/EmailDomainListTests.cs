using Taskpilot.API.Common;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="EmailDomainList"/> — the registration gate. Getting a
/// boundary wrong either blocks everyone or lets any domain in, so the edges are pinned.
/// </summary>
public class EmailDomainListTests
{
    [Fact]
    public void EmptyConfig_IsOff_AndAllowsAnyDomain()
    {
        var list = EmailDomainList.Parse("");

        Assert.False(list.IsEnabled);
        Assert.True(list.IsAllowed("anyone@wherever.com"));
    }

    [Fact]
    public void AllowsOnlyListedDomains_CaseInsensitive()
    {
        var list = EmailDomainList.Parse("acme.com, acme.io");

        Assert.True(list.IsEnabled);
        Assert.True(list.IsAllowed("alice@acme.com"));
        Assert.True(list.IsAllowed("BOB@ACME.IO"));       // case-insensitive
        Assert.False(list.IsAllowed("mallory@evil.com")); // not listed
    }

    [Fact]
    public void StripsLeadingAt_AndIgnoresBlanks()
    {
        var list = EmailDomainList.Parse("@acme.com, , ,  @acme.io  ");

        Assert.Equal(2, list.Domains.Count);
        Assert.True(list.IsAllowed("x@acme.com"));
        Assert.True(list.IsAllowed("y@acme.io"));
    }

    [Fact]
    public void MatchesTheDomainAfterTheLastAt()
    {
        var list = EmailDomainList.Parse("acme.com");

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
        var list = EmailDomainList.Parse("acme.com");
        Assert.False(list.IsAllowed(email));
    }

    [Fact]
    public void Domains_AreDeduplicatedAndLowerCased()
    {
        var list = EmailDomainList.Parse("Acme.com, ACME.COM, acme.com");
        Assert.Single(list.Domains);
        Assert.Contains("acme.com", list.Domains);
    }

    // --- denylist semantics (Contains): an empty list blocks nothing ---

    [Fact]
    public void Contains_OnAnEmptyList_IsAlwaysFalse_SoNothingIsBlocked()
    {
        var list = EmailDomainList.Parse("");

        Assert.False(list.Contains("anyone@wherever.com"));
        Assert.False(list.Contains(null));
    }

    [Fact]
    public void Contains_MatchesOnlyListedDomains_CaseInsensitive()
    {
        var list = EmailDomainList.Parse("spam.example, junk.example");

        Assert.True(list.Contains("bot@spam.example"));
        Assert.True(list.Contains("BOT@JUNK.EXAMPLE"));
        Assert.False(list.Contains("alice@acme.com"));
    }

    [Fact]
    public void Contains_UsesTheDomainAfterTheLastAt()
    {
        var list = EmailDomainList.Parse("spam.example");

        // An extra '@' in the local part must not smuggle a blocked domain past the check.
        Assert.True(list.Contains("weird@name@spam.example"));
        Assert.False(list.Contains("weird@spam.example@acme.com"));
    }
}
