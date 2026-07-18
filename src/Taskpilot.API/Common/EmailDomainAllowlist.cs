namespace Taskpilot.API.Common;

/// <summary>
/// An allowlist of email domains permitted to self-register, e.g. "acme.com, acme.io".
/// An empty list means "off": any domain may register. Parsing is lenient — a leading "@"
/// is stripped, entries are trimmed and lower-cased, and blanks are ignored — so a config
/// typo can never take registration down.
/// </summary>
public sealed class EmailDomainAllowlist
{
    private readonly HashSet<string> _domains;

    private EmailDomainAllowlist(HashSet<string> domains) => _domains = domains;

    /// <summary>True when at least one domain is configured; otherwise the list is off.</summary>
    public bool IsEnabled => _domains.Count > 0;

    /// <summary>The parsed, cleaned domains (lower-cased, de-duplicated) — used to store a canonical value.</summary>
    public IReadOnlyCollection<string> Domains => _domains;

    /// <summary>Parses a comma-separated list of domains. Blank/whitespace entries are ignored.</summary>
    public static EmailDomainAllowlist Parse(string? csv)
    {
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Accept "@acme.com" or "acme.com"; store the bare domain, lower-cased.
            var domain = raw.TrimStart('@').Trim().ToLowerInvariant();
            if (domain.Length > 0)
                domains.Add(domain);
        }
        return new EmailDomainAllowlist(domains);
    }

    /// <summary>
    /// True if the email may register: always true when the list is off, otherwise true only
    /// when the email's domain (the part after the last '@') is on the list.
    /// </summary>
    public bool IsAllowed(string? email)
    {
        if (!IsEnabled) return true;
        if (string.IsNullOrWhiteSpace(email)) return false;

        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1) return false;   // no domain part

        var domain = email[(at + 1)..].Trim().ToLowerInvariant();
        return _domains.Contains(domain);
    }
}
