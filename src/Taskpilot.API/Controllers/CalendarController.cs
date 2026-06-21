using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public CalendarController(ITaskService tasks)
    {
        _tasks = tasks;
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
        var toDate = to.HasValue
            ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc)
            : fromDate.AddMonths(1);

        var result = await _tasks.GetCalendarTasksAsync(userId.Value, fromDate, toDate);
        return Ok(result.Value);
    }
}
