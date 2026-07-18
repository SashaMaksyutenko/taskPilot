namespace Taskpilot.API.Common;

/// <summary>
/// A parsed list of email domains, e.g. "acme.com, acme.io". Used for BOTH registration
/// controls, which read the same list two different ways:
/// <list type="bullet">
///   <item>allowlist — <see cref="IsAllowed"/>: an empty list allows everything;</item>
///   <item>denylist — <see cref="Contains"/>: an empty list blocks nothing.</item>
/// </list>
/// Parsing is lenient — a leading "@" is stripped, entries are trimmed and lower-cased,
/// and blanks are ignored — so a typo can never take registration down.
/// </summary>
public sealed class EmailDomainList
{
    private readonly HashSet<string> _domains;

    private EmailDomainList(HashSet<string> domains) => _domains = domains;

    /// <summary>True when at least one domain is configured; otherwise the list is empty.</summary>
    public bool IsEnabled => _domains.Count > 0;

    /// <summary>The parsed, cleaned domains (lower-cased, de-duplicated) — used to store a canonical value.</summary>
    public IReadOnlyCollection<string> Domains => _domains;

    /// <summary>Parses a comma-separated list of domains. Blank/whitespace entries are ignored.</summary>
    public static EmailDomainList Parse(string? csv)
    {
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Accept "@acme.com" or "acme.com"; store the bare domain, lower-cased.
            var domain = raw.TrimStart('@').Trim().ToLowerInvariant();
            if (domain.Length > 0)
                domains.Add(domain);
        }
        return new EmailDomainList(domains);
    }

    /// <summary>
    /// True when the email's domain is on this list. Used for the DENYLIST: an empty list
    /// contains nothing, so nothing is blocked.
    /// </summary>
    public bool Contains(string? email) => Domain(email) is { } domain && _domains.Contains(domain);

    /// <summary>
    /// ALLOWLIST check: always true when the list is empty (the control is off), otherwise
    /// true only when the email's domain is on the list.
    /// </summary>
    public bool IsAllowed(string? email) => !IsEnabled || Contains(email);

    /// <summary>
    /// Extracts the domain part (after the LAST '@'), lower-cased — so an extra '@' in the
    /// local part cannot fool the check. Null when the email has no usable domain.
    /// </summary>
    private static string? Domain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1) return null;

        return email[(at + 1)..].Trim().ToLowerInvariant();
    }
}
