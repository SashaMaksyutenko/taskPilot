using System.Text.RegularExpressions;

namespace Taskpilot.API.Common;

/// <summary>
/// Extracts @mentions from free text. A mention is <c>@token</c> where token matches a
/// candidate user's name with whitespace removed, case-insensitively (e.g. "@Sasha1"
/// or "@JohnDoe"). Matching is scoped by the caller to the relevant audience.
/// </summary>
public static partial class MentionParser
{
    [GeneratedRegex(@"@([A-Za-z0-9_]+)")]
    private static partial Regex TokenRegex();

    /// <summary>Returns the ids of candidates mentioned in the body.</summary>
    public static HashSet<Guid> Extract(string body, IEnumerable<(Guid Id, string Name)> candidates)
    {
        var tokens = TokenRegex().Matches(body)
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .ToHashSet();

        var matched = new HashSet<Guid>();
        if (tokens.Count == 0)
            return matched;

        foreach (var (id, name) in candidates)
        {
            var normalized = new string(name.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();
            if (normalized.Length > 0 && tokens.Contains(normalized))
                matched.Add(id);
        }

        return matched;
    }
}
