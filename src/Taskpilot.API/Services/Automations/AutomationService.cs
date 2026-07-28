using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Automations;
using Taskpilot.API.Models;
using Taskpilot.Contracts;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class AutomationService : IAutomationService
{
    private readonly TaskpilotDbContext _context;
    private readonly INotificationService _notifications;
    private readonly ILogger<AutomationService> _logger;

    public AutomationService(TaskpilotDbContext context, INotificationService notifications, ILogger<AutomationService> logger)
    {
        _context = context;
        _notifications = notifications;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<List<AutomationRuleDto>>> GetRulesAsync(Guid userId, Guid projectId)
    {
        if (!await ProjectAccess.CanAccessAsync(_context, projectId, userId))
            return Result<List<AutomationRuleDto>>.Fail("Project not found.");

        var rules = await _context.AutomationRules
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        return Result<List<AutomationRuleDto>>.Ok(rules.Select(MapDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<AutomationRuleDto>> CreateRuleAsync(Guid userId, Guid projectId, SaveAutomationRuleDto dto)
    {
        if (!await IsOwnerAsync(projectId, userId))
            return Result<AutomationRuleDto>.Fail("Only the project owner can manage automations.");

        var validated = await ValidateAsync(projectId, dto);
        if (!validated.Succeeded)
            return Result<AutomationRuleDto>.Fail(validated.Error!);
        var v = validated.Value!;

        var rule = new AutomationRule
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? DefaultName(v) : dto.Name.Trim(),
            IsEnabled = dto.IsEnabled,
            Trigger = v.Trigger,
            TriggerStatus = v.TriggerStatus,
            Action = v.Action,
            ActionValue = v.ActionValue,
            CreatedAt = DateTime.UtcNow,
        };
        _context.AutomationRules.Add(rule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Automation rule created. RuleId: {RuleId}, ProjectId: {ProjectId}", rule.Id, projectId);
        return Result<AutomationRuleDto>.Ok(MapDto(rule));
    }

    /// <inheritdoc />
    public async Task<Result<AutomationRuleDto>> UpdateRuleAsync(Guid userId, Guid ruleId, SaveAutomationRuleDto dto)
    {
        var rule = await _context.AutomationRules.FirstOrDefaultAsync(r => r.Id == ruleId);
        if (rule is null)
            return Result<AutomationRuleDto>.Fail("Automation rule not found.");
        if (!await IsOwnerAsync(rule.ProjectId, userId))
            return Result<AutomationRuleDto>.Fail("Only the project owner can manage automations.");

        var validated = await ValidateAsync(rule.ProjectId, dto);
        if (!validated.Succeeded)
            return Result<AutomationRuleDto>.Fail(validated.Error!);
        var v = validated.Value!;

        rule.Name = string.IsNullOrWhiteSpace(dto.Name) ? DefaultName(v) : dto.Name.Trim();
        rule.IsEnabled = dto.IsEnabled;
        rule.Trigger = v.Trigger;
        rule.TriggerStatus = v.TriggerStatus;
        rule.Action = v.Action;
        rule.ActionValue = v.ActionValue;
        await _context.SaveChangesAsync();

        return Result<AutomationRuleDto>.Ok(MapDto(rule));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteRuleAsync(Guid userId, Guid ruleId)
    {
        var rule = await _context.AutomationRules.FirstOrDefaultAsync(r => r.Id == ruleId);
        if (rule is null)
            return Result.Fail("Automation rule not found.");
        if (!await IsOwnerAsync(rule.ProjectId, userId))
            return Result.Fail("Only the project owner can manage automations.");

        _context.AutomationRules.Remove(rule);
        await _context.SaveChangesAsync();
        return Result.Ok();
    }

    /// <inheritdoc />
    public Task RunOnTaskCreatedAsync(ProjectTask task) => RunAsync(task, AutomationTrigger.OnTaskCreated);

    /// <inheritdoc />
    public Task RunOnTaskStatusChangedAsync(ProjectTask task) => RunAsync(task, AutomationTrigger.OnTaskStatusChanged);

    // --- engine ---

    private async Task RunAsync(ProjectTask task, AutomationTrigger trigger)
    {
        var rules = await _context.AutomationRules
            .Where(r => r.ProjectId == task.ProjectId && r.IsEnabled && r.Trigger == trigger)
            .ToListAsync();
        if (rules.Count == 0)
            return;

        var ownerId = await _context.Projects.Where(p => p.Id == task.ProjectId).Select(p => p.OwnerId).FirstOrDefaultAsync();
        var mutated = false;

        foreach (var rule in rules)
        {
            // A status-change rule can be narrowed to a specific target status.
            if (trigger == AutomationTrigger.OnTaskStatusChanged && rule.TriggerStatus is { } wanted && wanted != task.Status)
                continue;

            try
            {
                mutated |= await ExecuteAsync(rule, task, ownerId);
            }
            catch (Exception ex)
            {
                // One faulty rule must not break the task operation that triggered it.
                _logger.LogError(ex, "Automation rule {RuleId} failed on task {TaskId}.", rule.Id, task.Id);
            }
        }

        if (mutated)
            await _context.SaveChangesAsync();
    }

    /// <summary>Runs one rule's action. Returns true when it changed the tracked task/added an entity to save.</summary>
    private async Task<bool> ExecuteAsync(AutomationRule rule, ProjectTask task, Guid ownerId)
    {
        switch (rule.Action)
        {
            case AutomationAction.SetPriority when Enum.TryParse<TaskPriority>(rule.ActionValue, ignoreCase: true, out var priority):
                task.Priority = priority;
                task.UpdatedAt = DateTime.UtcNow;
                return true;

            case AutomationAction.AssignToUser when Guid.TryParse(rule.ActionValue, out var assigneeId):
                // Only assign to someone who still belongs to the project.
                var isMember = await _context.Projects.AnyAsync(p => p.Id == task.ProjectId
                    && (p.OwnerId == assigneeId || p.Members.Any(m => m.UserId == assigneeId)));
                if (!isMember) return false;
                task.AssigneeId = assigneeId;
                task.UpdatedAt = DateTime.UtcNow;
                return true;

            case AutomationAction.NotifyOwner:
                await _notifications.CreateAsync(ownerId, NotificationType.Task,
                    $"🤖 \"{rule.Name}\": task \"{task.Title}\" triggered an automation.",
                    $"/projects/{task.ProjectId}");
                return false;

            case AutomationAction.AddComment when !string.IsNullOrWhiteSpace(rule.ActionValue):
                _context.TaskComments.Add(new TaskComment
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    AuthorId = ownerId,
                    Body = rule.ActionValue!.Trim(),
                    CreatedAt = DateTime.UtcNow,
                });
                return true;

            default:
                return false;
        }
    }

    // --- helpers ---

    private Task<bool> IsOwnerAsync(Guid projectId, Guid userId) =>
        _context.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == userId);

    private record Validated(AutomationTrigger Trigger, ProjectTaskStatus? TriggerStatus, AutomationAction Action, string? ActionValue);

    /// <summary>Validates and normalises a rule payload, resolving the enum/parameter combinations.</summary>
    private async Task<Result<Validated>> ValidateAsync(Guid projectId, SaveAutomationRuleDto dto)
    {
        if (!Enum.TryParse<AutomationTrigger>(dto.Trigger, ignoreCase: true, out var trigger))
            return Result<Validated>.Fail("Invalid trigger.");
        if (!Enum.TryParse<AutomationAction>(dto.Action, ignoreCase: true, out var action))
            return Result<Validated>.Fail("Invalid action.");

        ProjectTaskStatus? triggerStatus = null;
        if (trigger == AutomationTrigger.OnTaskStatusChanged && !string.IsNullOrWhiteSpace(dto.TriggerStatus))
        {
            if (!Enum.TryParse<ProjectTaskStatus>(dto.TriggerStatus, ignoreCase: true, out var status))
                return Result<Validated>.Fail("Invalid trigger status.");
            triggerStatus = status;
        }

        string? actionValue = dto.ActionValue?.Trim();
        switch (action)
        {
            case AutomationAction.SetPriority:
                if (!Enum.TryParse<TaskPriority>(actionValue, ignoreCase: true, out var p))
                    return Result<Validated>.Fail("Action value must be a priority (Low, Medium or High).");
                actionValue = p.ToString();
                break;

            case AutomationAction.AssignToUser:
                if (!Guid.TryParse(actionValue, out var assigneeId))
                    return Result<Validated>.Fail("Action value must be a user id to assign to.");
                var isMember = await _context.Projects.AnyAsync(p2 => p2.Id == projectId
                    && (p2.OwnerId == assigneeId || p2.Members.Any(m => m.UserId == assigneeId)));
                if (!isMember)
                    return Result<Validated>.Fail("The assignee must be a member of the project.");
                break;

            case AutomationAction.AddComment:
                if (string.IsNullOrWhiteSpace(actionValue))
                    return Result<Validated>.Fail("Action value (the comment text) is required.");
                break;

            case AutomationAction.NotifyOwner:
                actionValue = null; // no parameter
                break;
        }

        return Result<Validated>.Ok(new Validated(trigger, triggerStatus, action, actionValue));
    }

    private static string DefaultName(Validated v)
    {
        var when = v.Trigger == AutomationTrigger.OnTaskCreated
            ? "task created"
            : v.TriggerStatus is { } s ? $"moved to {s}" : "status changed";
        return $"When {when} → {v.Action}";
    }

    private static AutomationRuleDto MapDto(AutomationRule r) => new()
    {
        Id = r.Id,
        ProjectId = r.ProjectId,
        Name = r.Name,
        IsEnabled = r.IsEnabled,
        Trigger = r.Trigger.ToString(),
        TriggerStatus = r.TriggerStatus?.ToString(),
        Action = r.Action.ToString(),
        ActionValue = r.ActionValue,
        CreatedAt = r.CreatedAt,
    };
}
