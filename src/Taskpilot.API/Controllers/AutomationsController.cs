using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taskpilot.API.DTOs.Automations;
using Taskpilot.API.Services;

namespace Taskpilot.API.Controllers;

/// <summary>Project automations ("robots"): list rules per project; the owner creates/edits/deletes them.</summary>
[ApiController]
[Authorize]
public class AutomationsController : BaseApiController
{
    private readonly IAutomationService _automations;

    public AutomationsController(IAutomationService automations)
    {
        _automations = automations;
    }

    /// <summary>Lists a project's automation rules.</summary>
    [HttpGet("api/projects/{projectId:guid}/automations")]
    public async Task<IActionResult> GetForProject(Guid projectId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _automations.GetRulesAsync(userId.Value, projectId);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Creates an automation rule in a project (owner only).</summary>
    [HttpPost("api/projects/{projectId:guid}/automations")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] SaveAutomationRuleDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _automations.CreateRuleAsync(userId.Value, projectId, dto);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Updates an automation rule (owner only).</summary>
    [HttpPut("api/automations/{ruleId:guid}")]
    public async Task<IActionResult> Update(Guid ruleId, [FromBody] SaveAutomationRuleDto dto)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _automations.UpdateRuleAsync(userId.Value, ruleId, dto);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Deletes an automation rule (owner only).</summary>
    [HttpDelete("api/automations/{ruleId:guid}")]
    public async Task<IActionResult> Delete(Guid ruleId)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _automations.DeleteRuleAsync(userId.Value, ruleId);
        return result.Succeeded ? NoContent() : BadRequest(new { error = result.Error });
    }
}
