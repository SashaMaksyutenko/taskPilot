using System.Globalization;
using System.Text.Json;

namespace Taskpilot.API.Services.Assistant;

/// <summary>
/// Shared helpers for the assistant's write toolboxes: parsing the model's JSON arguments
/// (which are best-effort and may be missing or the wrong type) and serialising tool results.
/// Every reader tolerates a bad shape and returns null/empty rather than throwing, so a
/// malformed tool call degrades into a friendly error instead of a 500.
/// </summary>
internal static class AssistantArgs
{
    public static string Json(object value) => JsonSerializer.Serialize(value);

    public static JsonElement Parse(string json)
    {
        try { return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json).RootElement.Clone(); }
        catch (JsonException) { return JsonDocument.Parse("{}").RootElement.Clone(); }
    }

    public static string? Str(JsonElement o, string prop) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public static bool? Bool(JsonElement o, string prop)
    {
        if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty(prop, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(v.GetString(), out var b) => b,
            _ => null,
        };
    }

    public static int? Int(JsonElement o, string prop)
    {
        if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s)) return s;
        return null;
    }

    public static decimal? Dec(JsonElement o, string prop)
    {
        if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s)) return s;
        return null;
    }

    public static DateTime? DateOpt(JsonElement o, string prop)
    {
        var s = Str(o, prop);
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
    }

    public static List<string> StrArray(JsonElement o, string prop)
    {
        if (o.ValueKind == JsonValueKind.Object && o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array)
            return v.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        return new List<string>();
    }

    /// <summary>Maps free-form priority text to the canonical Low/Medium/High, or null if unset/unknown.</summary>
    public static string? NormalizePriority(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority)) return null;
        return priority.Trim().ToLowerInvariant() switch
        {
            "low" => "Low",
            "high" => "High",
            "medium" => "Medium",
            _ => null,
        };
    }

    /// <summary>Maps free-form status text to a canonical Kanban status, or null if unknown.</summary>
    public static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        return status.Trim().Replace(" ", "").ToLowerInvariant() switch
        {
            "backlog" => "Backlog",
            "inprogress" or "todo" or "doing" => "InProgress",
            "review" => "Review",
            "done" or "complete" or "completed" => "Done",
            _ => null,
        };
    }
}
