using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Automations;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <summary>
/// Project automations ("robots"): owner-managed rules that run an action when a task event
/// fires. The task service calls the two Run* hooks after it creates or re-statuses a task.
/// </summary>
public interface IAutomationService
{
    /// <summary>Lists a project's rules (any member with access can view them).</summary>
    Task<Result<List<AutomationRuleDto>>> GetRulesAsync(Guid userId, Guid projectId);

    /// <summary>Creates a rule (project owner only).</summary>
    Task<Result<AutomationRuleDto>> CreateRuleAsync(Guid userId, Guid projectId, SaveAutomationRuleDto dto);

    /// <summary>Updates a rule (project owner only).</summary>
    Task<Result<AutomationRuleDto>> UpdateRuleAsync(Guid userId, Guid ruleId, SaveAutomationRuleDto dto);

    /// <summary>Deletes a rule (project owner only).</summary>
    Task<Result> DeleteRuleAsync(Guid userId, Guid ruleId);

    /// <summary>Runs the OnTaskCreated rules of the task's project.</summary>
    Task RunOnTaskCreatedAsync(ProjectTask task);

    /// <summary>Runs the OnTaskStatusChanged rules of the task's project (respecting the status filter).</summary>
    Task RunOnTaskStatusChangedAsync(ProjectTask task);
}
