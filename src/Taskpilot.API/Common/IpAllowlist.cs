using System.Net;
using System.Net.Sockets;

namespace Taskpilot.API.Common;

/// <summary>
/// An allowlist of IP addresses and CIDR ranges, e.g. "203.0.113.5, 10.0.0.0/8, 2001:db8::/32".
/// Parsing never throws — an unparseable entry is skipped rather than taking the API down on
/// a config typo. An empty list means "off": everything is allowed.
/// </summary>
public sealed class IpAllowlist
{
    private readonly List<(byte[] Network, int Prefix)> _entries;

    private IpAllowlist(List<(byte[], int)> entries) => _entries = entries;

    /// <summary>True when at least one valid entry was configured; otherwise the list is off.</summary>
    public bool IsEnabled => _entries.Count > 0;

    /// <summary>Parses a comma-separated list of IPs and/or CIDR ranges. Bad entries are ignored.</summary>
    public static IpAllowlist Parse(string? csv)
    {
        var entries = new List<(byte[], int)>();

        foreach (var raw in (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var slash = raw.IndexOf('/');
            var addressPart = slash < 0 ? raw : raw[..slash];
            if (!IPAddress.TryParse(addressPart, out var address)) continue;

            address = Normalize(address);
            var bits = address.GetAddressBytes().Length * 8;

            // No mask = a single host (/32 for IPv4, /128 for IPv6).
            var prefix = bits;
            if (slash >= 0 && (!int.TryParse(raw[(slash + 1)..], out prefix) || prefix < 0 || prefix > bits))
                continue;

            entries.Add((address.GetAddressBytes(), prefix));
        }

        return new IpAllowlist(entries);
    }

    /// <summary>Whether the address falls inside any configured entry. Always true when off.</summary>
    public bool IsAllowed(IPAddress? ip)
    {
        if (!IsEnabled) return true;
        if (ip is null) return false;

        var bytes = Normalize(ip).GetAddressBytes();
        foreach (var (network, prefix) in _entries)
        {
            // An IPv4 entry can never match an IPv6 address (and vice versa).
            if (network.Length != bytes.Length) continue;
            if (MatchesPrefix(bytes, network, prefix)) return true;
        }

        return false;
    }

    /// <summary>
    /// Kestrel reports IPv4 clients as IPv4-mapped IPv6 (::ffff:127.0.0.1), so compare like
    /// with like — otherwise "127.0.0.1" in config would never match a real local request.
    /// </summary>
    private static IPAddress Normalize(IPAddress ip) =>
        ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    private static bool MatchesPrefix(byte[] address, byte[] network, int prefixBits)
    {
        var fullBytes = prefixBits / 8;
        for (var i = 0; i < fullBytes; i++)
            if (address[i] != network[i]) return false;

        var remainingBits = prefixBits % 8;
        if (remainingBits == 0) return true;

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (address[fullBytes] & mask) == (network[fullBytes] & mask);
    }
}
