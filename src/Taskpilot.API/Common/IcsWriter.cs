using System.Text;
using Taskpilot.API.DTOs.Calendar;

namespace Taskpilot.API.Common;

/// <summary>
/// Builds a minimal RFC 5545 iCalendar (.ics) document from calendar tasks, so users
/// can import their task deadlines into Google/Apple/Outlook calendars.
/// </summary>
public static class IcsWriter
{
    /// <summary>Renders the tasks as an iCalendar document (one one-hour VEVENT per deadline).</summary>
    public static string Build(IEnumerable<CalendarTaskDto> tasks, string baseUrl)
    {
        var sb = new StringBuilder();
        // Lines are separated by CRLF as the spec requires.
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//TaskPilot//Calendar//EN\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
        foreach (var task in tasks)
        {
            var start = task.Deadline.ToUniversalTime();
            var end = start.AddHours(1);
            var url = $"{baseUrl.TrimEnd('/')}/projects/{task.ProjectId}";
            var description = $"Project: {task.ProjectName}\nStatus: {task.Status}\nPriority: {task.Priority}\n{url}";

            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append($"UID:{task.Id}@taskpilot\r\n");
            sb.Append($"DTSTAMP:{stamp}\r\n");
            sb.Append($"DTSTART:{start:yyyyMMdd'T'HHmmss'Z'}\r\n");
            sb.Append($"DTEND:{end:yyyyMMdd'T'HHmmss'Z'}\r\n");
            sb.Append($"SUMMARY:{Escape(task.Title)}\r\n");
            sb.Append($"DESCRIPTION:{Escape(description)}\r\n");
            sb.Append("END:VEVENT\r\n");
        }

        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    /// <summary>Escapes iCalendar TEXT values per RFC 5545 (backslash, semicolon, comma, newlines).</summary>
    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n");
}
