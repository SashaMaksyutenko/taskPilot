using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Projects;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Bulk-import tasks into a project from CSV (counterpart to CSV export).</summary>
[ApiController]
[Authorize]
public class TaskImportController : BaseApiController
{
    private readonly ICsvImportService _import;

    public TaskImportController(ICsvImportService import)
    {
        _import = import;
    }

    /// <summary>Creates tasks from CSV text; returns how many were created/skipped.</summary>
    [HttpPost("api/projects/{projectId:guid}/tasks/import")]
    public async Task<IActionResult> Import(Guid projectId, [FromBody] ImportTasksDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _import.ImportTasksAsync(userId.Value, projectId, dto.Csv);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}
