using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>
/// Calendar endpoints: tasks with deadlines in a date range.
/// </summary>
[ApiController]
[Authorize]
[Route("api/calendar")]
public class CalendarController : BaseApiController
{
    private readonly ITaskService _tasks;
    private readonly ICalendarFeedService _feed;
    private readonly EmailOptions _emailOptions;

    public CalendarController(ITaskService tasks, ICalendarFeedService feed, IOptions<EmailOptions> emailOptions)
    {
        _tasks = tasks;
        _feed = feed;
        _emailOptions = emailOptions.Value;
    }

    /// <summary>
    /// Returns the current user's deadline tasks between ?from and ?to.
    /// Defaults to the next month when the range is omitted.
    /// </summary>
    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        // Query-string dates bind as Unspecified kind; PostgreSQL 'timestamptz'
        // requires UTC, so mark them as UTC before querying.
        var fromDate = from.HasValue
            ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc)
            : DateTime.UtcNow.Date;
        var toRaw = to.HasValue
            ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc)
            : fromDate.AddMonths(1);

        // Make the 'to' day inclusive: cover the whole day, not just its midnight.
        var toDate = toRaw.Date.AddDays(1).AddTicks(-1);

        var result = await _tasks.GetCalendarTasksAsync(userId.Value, fromDate, toDate);
        return Ok(result.Value);
    }

    /// <summary>Exports the user's deadline tasks (±1 year) as an iCalendar (.ics) file.</summary>
    [HttpGet("export.ics")]
    public async Task<IActionResult> ExportIcs()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var from = DateTime.UtcNow.AddYears(-1);
        var to = DateTime.UtcNow.AddYears(1);
        var result = await _tasks.GetCalendarTasksAsync(userId.Value, from, to);

        var ics = IcsWriter.Build(result.Value ?? new(), _emailOptions.FrontendBaseUrl);
        return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar", "taskpilot.ics");
    }

    /// <summary>Returns the user's private, auto-updating iCal subscription URL.</summary>
    [HttpGet("feed-url")]
    public async Task<IActionResult> FeedUrl()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var token = await _feed.GetOrCreateTokenAsync(userId.Value);
        return Ok(new { url = BuildFeedUrl(token) });
    }

    /// <summary>Regenerates the feed token (invalidates the old subscription URL).</summary>
    [HttpPost("feed-url/regenerate")]
    public async Task<IActionResult> RegenerateFeedUrl()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var token = await _feed.RegenerateTokenAsync(userId.Value);
        return Ok(new { url = BuildFeedUrl(token) });
    }

    /// <summary>Public auto-updating iCal feed identified by a secret token in the URL.</summary>
    [HttpGet("feed/{token}.ics")]
    [AllowAnonymous]
    public async Task<IActionResult> Feed(string token)
    {
        var userId = await _feed.ResolveUserIdAsync(token);
        if (userId is null) return NotFound();

        var from = DateTime.UtcNow.AddYears(-1);
        var to = DateTime.UtcNow.AddYears(1);
        var result = await _tasks.GetCalendarTasksAsync(userId.Value, from, to);

        var ics = IcsWriter.Build(result.Value ?? new(), _emailOptions.FrontendBaseUrl);
        return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar");
    }

    /// <summary>Absolute URL of this API's feed endpoint for the given token.</summary>
    private string BuildFeedUrl(string token) => $"{Request.Scheme}://{Request.Host}/api/calendar/feed/{token}.ics";
}
