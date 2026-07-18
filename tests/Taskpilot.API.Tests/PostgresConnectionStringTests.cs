using Taskpilot.API.Common;
using Xunit;

namespace Taskpilot.API.Tests;

/// <summary>
/// Unit tests for <see cref="PostgresConnectionString"/>. Getting this wrong means the
/// deployed app cannot reach its database at all, so the URI shapes managed hosts actually
/// hand out are pinned down here.
/// </summary>
public class PostgresConnectionStringTests
{
    // NOTE: every fixture below uses the reserved .example / .invalid domains and obviously
    // fake credentials. A realistic-looking host with a plausible password trips secret
    // scanners (GitGuardian flagged an earlier version of this file as a leak).
    [Fact]
    public void ConvertsAManagedHostStyleUri()
    {
        // Same shape a managed host publishes: sub-domain, custom port, generated database.
        var result = PostgresConnectionString.Normalize(
            "postgresql://postgres:examplepassword@containers.db.example:6543/railway");

        Assert.Contains("Host=containers.db.example", result);
        Assert.Contains("Port=6543", result);
        Assert.Contains("Database=railway", result);
        Assert.Contains("Username=postgres", result);
        Assert.Contains("Password=examplepassword", result);
        // Prefer, not Require: TLS is used when the server offers it, but a plain local
        // Postgres still connects instead of hard-failing at startup.
        Assert.Contains("SSL Mode=Prefer", result);
        // Managed Postgres serves a self-signed certificate, so the chain is not validated.
        Assert.Contains("Trust Server Certificate=true", result);
    }

    [Theory]
    [InlineData("require", "SSL Mode=Require")]
    [InlineData("disable", "SSL Mode=Disable")]
    [InlineData("verify-full", "SSL Mode=VerifyFull")]
    public void HonoursAnExplicitSslModeFromTheUri(string mode, string expected)
    {
        var result = PostgresConnectionString.Normalize($"postgresql://u:p@db.example:5432/app?sslmode={mode}");

        Assert.Contains(expected, result);
    }

    [Fact]
    public void DoesNotTrustBlindly_WhenCertificateVerificationWasAskedFor()
    {
        var result = PostgresConnectionString.Normalize(
            "postgresql://u:p@db.example:5432/app?sslmode=verify-full");

        // Asking to verify the certificate and then trusting any certificate is contradictory.
        Assert.DoesNotContain("Trust Server Certificate", result);
    }

    [Fact]
    public void IgnoresUnrelatedQueryParameters()
    {
        var result = PostgresConnectionString.Normalize(
            "postgresql://u:p@db.example:5432/app?connect_timeout=10&application_name=x");

        Assert.Contains("SSL Mode=Prefer", result);
        Assert.Contains("Database=app", result);
    }

    [Fact]
    public void AcceptsThePostgresScheme_AsWellAsPostgresql()
    {
        var result = PostgresConnectionString.Normalize("postgres://u:p@db.example:5432/app");

        Assert.Contains("Host=db.example", result);
        Assert.Contains("Database=app", result);
    }

    [Fact]
    public void DefaultsThePort_WhenTheUriOmitsIt()
    {
        var result = PostgresConnectionString.Normalize("postgresql://u:p@db.example/app");

        Assert.Contains("Port=5432", result);
    }

    [Fact]
    public void DecodesPercentEncodedCredentials()
    {
        // A password with '@' and '/' must survive the round trip, or auth fails in prod.
        var result = PostgresConnectionString.Normalize(
            "postgresql://us%40er:p%40ss%2Fword@db.example:5432/app");

        Assert.Contains("Username=us@er", result);
        Assert.Contains("Password=p@ss/word", result);
    }

    [Fact]
    public void LeavesAKeyValueConnectionStringUnchanged()
    {
        const string keyValue =
            "Host=localhost;Port=5433;Database=taskpilot;Username=postgres;Password=examplepassword";

        Assert.Equal(keyValue, PostgresConnectionString.Normalize(keyValue));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReturnsNull_ForBlankInput(string? input)
    {
        Assert.Null(PostgresConnectionString.Normalize(input));
    }

    [Theory]
    [InlineData("postgresql://")]              // no host or database
    [InlineData("postgresql://u:p@host:5432/")] // no database name
    public void ReturnsNull_ForAnUnusableUri(string input)
    {
        Assert.Null(PostgresConnectionString.Normalize(input));
    }
}
